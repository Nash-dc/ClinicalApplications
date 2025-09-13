using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        public async Task<PlanJson?> GeneratePlanStructuredAsync(
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
                You are a clinical exercise planning assistant specializing in post-breast cancer recovery.
                In developing the exercise plan, consider these evidence-based factors important for breast cancer survivors:
                - **Weight management (BMI):** Overweight survivors benefit from weight loss, improving quality of life and reducing complications. Encourage moderate exercise and diet for weight control, as this can improve prognosis and reduce recurrence risk.
                - **Upper limb function (shoulder ROM & strength):** After breast surgery, structured upper-body rehab (e.g. gentle range-of-motion exercises and progressive resistance training starting ~4–6 weeks post-op) improves shoulder mobility and strength without increasing lymphedema risk. Include appropriate arm/shoulder exercises to restore function and reduce pain.
                - **Psychological health (stress, HR/HRV):** Mental well-being is crucial. High stress or PTSD can elevate resting heart rate and lower heart rate variability (HRV), whereas relaxation and positive coping raise HRV. Incorporate stress-reduction techniques (like mindfulness meditation or deep-breathing exercises) to improve autonomic function, reduce fatigue, and boost mood.
                - **Sleep quality:** Many breast cancer survivors experience sleep disturbances, which correlate with pain, depression, and even survival outcomes. Emphasize good sleep hygiene and adequate rest, as better sleep supports recovery and overall health.
                Tailor the plan to the patient’s specific data (e.g. their BMI, comorbidities, treatment history, fatigue level), adjusting recommendations accordingly.
                Output MUST be a single JSON object ONLY (no markdown or prose).
                JSON schema:
                {
                  "version": "1.0",
                  "week": [
                    {"day":"Mon","steps":int,"active_minutes":int,"rpe":"x-y"},
                    … Tue..Sun …
                  ],
                  "safety_notes": ["...", "..."],
                  "pause_rule": "..."
                }
                Constraints:
                - steps ≤ MaxDailySteps; active_minutes ≤ MaxDailyActiveMinutes; rpe format "a-b" with 1 ≤ a ≤ b ≤ MaxRpe.
                - Provide 7 entries in "week" for Mon–Sun.
                - Include 3–5 concise safety_notes (incorporate relevant recovery tips as needed).
                - Provide a clear pause_rule (when to stop exercise and seek help, e.g. if severe symptoms occur).
                """;

            var user = $"""
                Patient:
                - Age {patient.Age} y; BMI {(double.IsNaN(bmi) ? "unknown" : bmi.ToString("0.0", CultureInfo.InvariantCulture))}
                - LVEF {patient.LVEF}%; HR {patient.HeartRate} bpm; rhythm {(patient.HeartRhythm == 1 ? "AF" : "sinus")}
                - LV geom: PWT {patient.PWT} cm; LAd {patient.LAd} cm; LVDd {patient.LVDd} cm; LVSd {patient.LVSd} cm
                - Onco-therapy: {therapyGroup} (AC={patient.AC}, antiHER2={patient.AntiHER2}, ACprev={patient.ACprev}, antiHER2prev={patient.AntiHER2prev})
                - Comorbidity score: {comorbidityScore}
                - Baseline: {baselineCapacity ?? "unknown"}
                - Restrictions: {restrictionsText}

                Recent 7-day:
                - steps/day: {(avgStepsPerDay?.ToString() ?? "unknown")}
                - active_minutes/day: {(avgActiveMinutesPerDay?.ToString() ?? "unknown")}
                - resting HR: {(avgRestingHr?.ToString() ?? "unknown")}
                - fatigue (1-5): {(avgFatigueScore?.ToString() ?? "unknown")}

                Guardrails:
                - MaxDailySteps={guardrails.MaxDailySteps}
                - MaxDailyActiveMinutes={guardrails.MaxDailyActiveMinutes}
                - MaxRpe={guardrails.MaxRpe}
                - MaxWeeklyIncreasePercent={guardrails.MaxWeeklyIncreasePercent}

                Return JSON only.
                """;

            var completion = await _chat.CompleteChatAsync(new ChatMessage[]
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            });

            var raw = (completion.Value.Content.Count > 0 && completion.Value.Content[0].Text is { } txt) ? txt : null;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var m = Regex.Match(raw, @"\{[\s\S]*\}");
            if (!m.Success) return null;
            var json = m.Value;

            PlanJson? plan;
            try
            {
                plan = JsonSerializer.Deserialize<PlanJson>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
            if (plan is null) return null;

            if (plan.week == null || plan.week.Count != 7) return null;

            var daysOrder = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var rpeRegex = new Regex(@"^\d-\d$");
            for (int i = 0; i < 7; i++)
            {
                var d = plan.week[i];
                if (!string.Equals(d.day, daysOrder[i], StringComparison.OrdinalIgnoreCase))
                    d.day = daysOrder[i];

                d.steps = Math.Max(0, Math.Min(guardrails.MaxDailySteps, d.steps));
                d.active_minutes = Math.Max(0, Math.Min(guardrails.MaxDailyActiveMinutes, d.active_minutes));

                if (!rpeRegex.IsMatch(d.rpe))
                    d.rpe = $"1-{guardrails.MaxRpe}";
                else
                {
                    var parts = d.rpe.Split('-');
                    int lo = int.Parse(parts[0]), hi = int.Parse(parts[1]);
                    lo = Math.Max(1, Math.Min(guardrails.MaxRpe, lo));
                    hi = Math.Max(lo, Math.Min(guardrails.MaxRpe, hi));
                    d.rpe = $"{lo}-{hi}";
                }
            }

            if (string.IsNullOrWhiteSpace(plan.pause_rule))
                plan.pause_rule = "Pause if chest pain, dizziness, or severe dyspnea occurs, and contact clinician.";

            if (plan.safety_notes == null || plan.safety_notes.Count == 0)
                plan.safety_notes = new List<string> { "Warm-up & cool-down", "Hydrate", "Stop if unusual symptoms" };

            return plan;
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
