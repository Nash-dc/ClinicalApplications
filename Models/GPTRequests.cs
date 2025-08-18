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

        public async Task<string> GeneratePersonalizedPlanAsync(
            CaseProfile caseProfile,
            RecentMetrics recentMetrics,
            PlanGuardrails guardrails)
        {
            var system = """
                    You are a clinical exercise planning assistant for post-cancer recovery.
                    Produce short, actionable weekly plans: daily step target, active minutes,
                    RPE intensity range, and 3–5 bullet safety notes.
                    Strictly avoid medical diagnosis or drug advice.
                    Output concise English, ≤ 180 words.
                """;

            var user = $"""
                Patient profile:
                - Cancer type: {caseProfile.CancerType}
                - Treatment timeline: {JoinTimeline(caseProfile.Treatments)}
                - Restrictions: {string.Join("; ", caseProfile.Restrictions ?? new List<string> { "none" })}
                - Baseline capacity (if known): {caseProfile.BaselineCapacity ?? "unknown"}

                Recent metrics (last 7 days):
                - Avg steps/day: {recentMetrics.AvgStepsPerDay}
                - Avg active minutes/day: {recentMetrics.AvgActiveMinutesPerDay}
                - Resting HR (avg): {recentMetrics.AvgRestingHr ?? 0}
                - Fatigue score (1-5, avg): {recentMetrics.AvgFatigueScore ?? 0}

                Safety guardrails (must obey):
                - Daily step target <= {guardrails.MaxDailySteps}
                - Daily active minutes <= {guardrails.MaxDailyActiveMinutes}
                - RPE range max <= {guardrails.MaxRpe}
                - Weekly step increase <= {guardrails.MaxWeeklyIncreasePercent}% vs current baseline
                - Must honor listed restrictions

                Task:
                1) Propose a one-week plan (Mon-Sun): daily step target, active minutes, RPE range.
                2) Provide 3–5 safety notes considering restrictions and fatigue.
                3) End with a one-line “when to pause and contact clinician” rule.
                4) Do NOT exceed guardrails. If baseline is low, use gradual progression.
                """;

            var completion = await _chat.CompleteChatAsync(new ChatMessage[]
            {
                new SystemChatMessage(system),
                new UserChatMessage(user)
            });

            string planText = (completion.Value.Content.Count > 0 && completion.Value.Content[0].Text is { } txt)
                ? txt
                : "Plan generation failed.";

            planText = ApplyLocalGuardrails(planText, recentMetrics, guardrails);
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


        private static string ApplyLocalGuardrails(
            string planText, RecentMetrics m, PlanGuardrails g)
        {
            // 这里留一个非常轻量的示例。实际项目里：
            // - 可以把计划文本解析为结构化（正则/JSON模式）
            // - 针对 steps/active/RPE 做逐项限制与回写
            // 现在仅做长度、安全提醒兜底。
            if (string.IsNullOrWhiteSpace(planText))
                return "Plan generation failed.";

            // 兜底附加安全提示（若模型遗漏）
            if (!planText.Contains("pause", StringComparison.OrdinalIgnoreCase))
            {
                planText += "\n\nSafety note: Pause activity if unusual chest pain, dizziness, or shortness of breath occurs.";
            }
            return planText;
        }

        private static string JoinTimeline(IEnumerable<TreatmentEvent> events)
        {
            if (events == null) return "N/A";
            return string.Join(", ", events.Select(e =>
                $"{e.Type} on {e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"));
        }
    }

    // ======= 辅助数据模型（可放到 Models/ 下独立文件）=======

    internal sealed class CaseProfile
    {
        public string CancerType { get; set; } = "Breast cancer";
        public List<TreatmentEvent> Treatments { get; set; } = new();
        public List<string>? Restrictions { get; set; }
        public string? BaselineCapacity { get; set; } // e.g., "walks 20–30 min/day"
    }

    internal sealed class TreatmentEvent
    {
        public string Type { get; set; } = ""; // e.g., "surgery", "chemo"
        public DateTime Date { get; set; }
    }

    internal sealed class RecentMetrics
    {
        public int AvgStepsPerDay { get; set; }
        public int AvgActiveMinutesPerDay { get; set; }
        public int? AvgRestingHr { get; set; }
        public int? AvgFatigueScore { get; set; } // 1–5
    }

    internal sealed class PlanGuardrails
    {
        public int MaxDailySteps { get; set; } = 10000;
        public int MaxDailyActiveMinutes { get; set; } = 45;
        public int MaxRpe { get; set; } = 5;
        public int MaxWeeklyIncreasePercent { get; set; } = 20;
    }
}
