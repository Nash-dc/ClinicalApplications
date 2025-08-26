using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClinicalApplications.Models
{
    internal class GPTRequests
    {
        private readonly ChatClient _chat;

        public GPTRequests(string model = "gpt-5")
        {
            var keyPath = Path.Combine(AppContext.BaseDirectory, "Assets", "GPTkey.txt");
            if (!File.Exists(keyPath))
                throw new FileNotFoundException("API key file not found.", keyPath);

            var apiKey = File.ReadAllText(keyPath).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("API key file is empty.");

            _chat = new ChatClient(model, apiKey);
        }

        /// <summary>
        /// 基于 Patient（临床静态信息）+ 可选近期指标 生成一周个性化运动计划
        /// </summary>
        /// <param name="patient">你的 Patient 实例（必填）</param>
        /// <param name="guardrails">本地安全边界（必填）</param>
        /// <param name="avgStepsPerDay">可选：近7日平均步数</param>
        /// <param name="avgActiveMinutesPerDay">可选：近7日平均活动分钟</param>
        /// <param name="avgRestingHr">可选：近7日静息心率</param>
        /// <param name="avgFatigueScore">可选：近7日疲劳评分(1-5)</param>
        /// <param name="baselineCapacity">可选：基线能力描述，如 "walks 20–30 min/day"</param>
        /// <param name="extraRestrictions">可选：额外限制/注意事项文本列表</param>
        public async Task<string> GeneratePersonalizedPlanAsync(
            Patient patient,
            PlanGuardrails guardrails,
            int? avgStepsPerDay = null,
            int? avgActiveMinutesPerDay = null,
            int? avgRestingHr = null,
            int? avgFatigueScore = null,
            string? baselineCapacity = null,
            IEnumerable<string>? extraRestrictions = null)
        {
            var (bmi, therapyGroup, comorbidityScore) = DerivePatientFeatures(patient);

            var restrictionsList = BuildRestrictions(patient, extraRestrictions);
            var restrictionsText = restrictionsList.Count == 0 ? "none" : string.Join("; ", restrictionsList);

            var system = """
                You are a clinical exercise planning assistant for post-cancer recovery.
                Produce short, actionable weekly plans: daily step target, active minutes,
                RPE intensity range, and 3–5 bullet safety notes.
                Strictly avoid medical diagnosis or drug advice.
                Output concise English, ≤ 180 words.
                """;

            var user = $"""
                Patient (breast cancer context):
                - Age: {patient.Age} years
                - BMI: {(double.IsNaN(bmi) ? "unknown" : bmi.ToString("0.0", CultureInfo.InvariantCulture))}
                - LVEF: {patient.LVEF} %
                - Heart rate: {patient.HeartRate} bpm, rhythm: {(patient.HeartRhythm == 1 ? "AF" : "sinus")}
                - LV geometry: PWT {patient.PWT} cm; LAd {patient.LAd} cm; LVDd {patient.LVDd} cm; LVSd {patient.LVSd} cm
                - Onco-therapy: {therapyGroup} (AC={patient.AC}, antiHER2={patient.AntiHER2}, ACprev={patient.ACprev}, antiHER2prev={patient.AntiHER2prev})
                - Comorbidity score: {comorbidityScore} (HTA, DL, DM, smoker/ex, CIprev, ICMprev, ARRprev, VALVprev, cxvalv)
                - Baseline capacity: {baselineCapacity ?? "unknown"}
                - Restrictions: {restrictionsText}

                Recent 7-day metrics (optional):
                - Avg steps/day: {(avgStepsPerDay?.ToString() ?? "unknown")}
                - Avg active minutes/day: {(avgActiveMinutesPerDay?.ToString() ?? "unknown")}
                - Resting HR (avg): {(avgRestingHr?.ToString() ?? "unknown")}
                - Fatigue score (1-5, avg): {(avgFatigueScore?.ToString() ?? "unknown")}

                Safety guardrails (must obey):
                - Daily step target <= {guardrails.MaxDailySteps}
                - Daily active minutes <= {guardrails.MaxDailyActiveMinutes}
                - RPE range max <= {guardrails.MaxRpe}
                - Weekly step increase <= {guardrails.MaxWeeklyIncreasePercent}% vs current baseline
                - Must honor listed restrictions

                Task:
                1) Propose a one-week plan (Mon–Sun): daily step target, active minutes, RPE range.
                2) Provide 3–5 safety notes considering therapy and comorbidities.
                3) End with a one-line “when to pause and contact clinician” rule.
                4) Do NOT exceed guardrails. Use gradual progression if baseline is low.
                """;

            var completion = await _chat.CompleteChatAsync(new ChatMessage[]
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            });

            string planText = (completion.Value.Content.Count > 0 && completion.Value.Content[0].Text is { } txt)
                ? txt
                : "Plan generation failed.";

            planText = ApplyLocalGuardrails(planText, guardrails);
            return planText;
        }

        public async Task<string> AskAsync(string prompt)
        {
            var completion = await _chat.CompleteChatAsync(prompt);
            var text = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text
                : "(No response)";
            return text ?? "(No response)";
        }

        private static (double bmi, string therapyGroup, int comorbidityScore) DerivePatientFeatures(Patient p)
        {
            double bmi = double.NaN;
            if (p.Height > 0) bmi = p.Weight / Math.Pow(p.Height / 100.0, 2);

            string therapyGroup = (p.AC, p.AntiHER2) switch
            {
                (0, 0) => "none",
                (1, 0) => "AC_only",
                (0, 1) => "antiHER2_only",
                (1, 1) => "AC_plus_antiHER2",
                _ => "unknown"
            };

            int comorbidityScore =
                Safe01(p.HTA) + Safe01(p.DL) + Safe01(p.DM) + Safe01(p.Smoker) + Safe01(p.ExSmoker) +
                Safe01(p.CIprev) + Safe01(p.ICMprev) + Safe01(p.ARRprev) + Safe01(p.VALVprev) + Safe01(p.Cxvalv);

            return (bmi, therapyGroup, comorbidityScore);

            static int Safe01(int v) => v == 1 ? 1 : 0;
        }

        private static List<string> BuildRestrictions(Patient p, IEnumerable<string>? extra)
        {
            var r = new List<string>();

            if (p.AC == 1) r.Add("avoid high-intensity bouts during anthracycline cycles");
            if (p.AntiHER2 == 1) r.Add("monitor for dyspnea/fatigue during anti-HER2 therapy");
            if (p.HeartRhythm == 1) r.Add("avoid sudden vigorous effort due to AF");
            if (p.LVEF < 50) r.Add("keep RPE low-to-moderate; no maximal effort");
            if (p.CIprev == 1) r.Add("prior cardiac insufficiency—progress very gradually");
            if (p.RTprev == 1) r.Add("history of thoracic RT—watch for chest discomfort");
            if (p.DM == 1) r.Add("monitor glucose and foot care during walking sessions");
            if (p.HTA == 1) r.Add("avoid Valsalva; ensure BP well-controlled");

            if (extra != null)
                r.AddRange(extra.Where(s => !string.IsNullOrWhiteSpace(s)));

            return r.Distinct().ToList();
        }

        private static string ApplyLocalGuardrails(string planText, PlanGuardrails g)
        {
            if (string.IsNullOrWhiteSpace(planText))
                return "Plan generation failed.";

            if (!planText.Contains("pause", StringComparison.OrdinalIgnoreCase))
                planText += "\n\nSafety note: Pause activity if unusual chest pain, dizziness, or shortness of breath occurs.";
            return planText;
        }
    }

    internal sealed class PlanGuardrails
    {
        public int MaxDailySteps { get; set; } = 10000;
        public int MaxDailyActiveMinutes { get; set; } = 45;
        public int MaxRpe { get; set; } = 5;
        public int MaxWeeklyIncreasePercent { get; set; } = 20;
    }
}
