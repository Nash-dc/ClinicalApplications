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
                You are a highly knowledgeable AI agent specializing in post-surgery rehabilitation exercise plans for breast cancer survivors. Your purpose is to generate a personalized weekly exercise plan that helps the patient recover safely and improve their fitness, based on their clinical profile and recent wearable data.

                Expected Input
                The user will provide patient-specific data, including:
                - Clinical information: e.g. Body Mass Index (BMI), Left Ventricular Ejection Fraction (LVEF), resting Heart Rate (HR), reported fatigue level, surgery side (which side breast surgery was performed on), shoulder range of motion or mobility, and any other relevant clinical details.
                - Recent wearable data (7 days): e.g. daily step counts, Heart Rate Variability (HRV), total sleep duration each night, sleep quality metrics (such as number of awakenings, deep sleep percentage), etc.
                You can assume the input will be structured or described clearly, providing values for these parameters (or indicating if any are missing).

                Expected Output Structure
                You must output a single JSON object with the following structure and fields:

                {
                  "version": "1.0",
                  "week": [
                    { "day": "Mon", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Tue", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Wed", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Thu", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Fri", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Sat", "steps": 0, "active_minutes": 0, "rpe": "1-2" },
                    { "day": "Sun", "steps": 0, "active_minutes": 0, "rpe": "1-2" }
                  ],
                  "safety_notes": [ "string", ... ],
                  "pause_rule": "string",
                  "weight_notes": [ "string", ... ],
                  "upper_limb_notes": [ "string", ... ],
                  "psychological_notes": [ "string", ... ],
                  "sleep_notes": [ "string", ... ]
                }

                Guidelines and Requirements
                When generating the JSON plan, strictly follow these requirements:
                - JSON format only: Provide the output only as a JSON object matching the above structure. Do not include any explanatory text, markdown, or comments—just the JSON.
                - Use all input data: Tailor every part of the plan to the specific patient information given. Do not produce generic or template advice; the content should reflect the patient's actual BMI, fitness level, fatigue, etc. If certain expected data is missing, use the string "unknown" in its place within the notes.
                - Complete all fields: Ensure every field (week, safety_notes, pause_rule, weight_notes, upper_limb_notes, psychological_notes, sleep_notes) is present. Each "notes" category must contain at least 1 and at most 3 items in its array.
                - Consistency and clarity: Make sure the plan is realistic and consistent with a breast cancer survivor’s rehabilitation needs. All text should be in clear, concise English.

                Important Considerations for Each Category
                - Safety Notes: Include general safety precautions based on the patient’s health data. For example, consider cardiac function (e.g. if LVEF is low, recommend close monitoring of intensity), any surgery-related limitations, or other medical advice. Ensure the patient knows how to exercise safely and avoid injury or complications (such as lymphedema precautions).
                - Pause Rule: Define a clear rule for when the patient should stop or pause exercise. For instance, advise to pause if they experience chest pain, dizziness, extreme shortness of breath, concerning heart rate changes, or swelling in the affected arm. This should be a single, concise sentence.
                - Weight Notes: Provide guidance related to the patient’s BMI and body weight. If the patient is overweight or obese (high BMI), include recommended interventions (from evidence-based guidelines) such as gradual weight loss through diet and exercise. If the patient is underweight or normal BMI, focus on maintaining healthy weight and muscle mass. Tie the advice to their BMI value (or "unknown" if not provided) and suggest appropriate lifestyle or nutritional interventions.
                - Upper Limb Notes: Give advice on arm and shoulder exercises and precautions, considering the surgery side and shoulder mobility. For example, if the surgery was on the right side, caution against over-straining that arm. If shoulder range of motion is limited, include gentle stretching or physical therapy exercises to improve mobility. Address lymphedema risk: if lymph nodes were removed or swelling is a concern, mention compression garments or avoiding heavy lifting with the affected arm initially.
                - Psychological Notes: Offer recommendations to support mental and emotional well-being during recovery. Use indicators like fatigue levels, HR, and HRV as clues: for example, high fatigue or low HRV may indicate stress or poor recovery, so suggest relaxation techniques, breathing exercises, or short meditation. Encourage positive mindset and stress management strategies, especially if the patient’s data or self-reported mood suggests they are struggling. Tailor this to the patient’s reported mental state and physiological stress signals.
                - Sleep Notes: Provide advice to improve or maintain healthy sleep, based on the patient’s sleep data from the past week. Consider total sleep time, number of awakenings, and deep sleep ratio. For example, if the patient is getting insufficient sleep or many interruptions, suggest improving sleep hygiene (consistent bedtime, a calm routine, avoiding screens before bed). If deep sleep percentage is low, recommend relaxation before bed or moderate exercise earlier in the day to aid sleep quality. Emphasize the importance of good sleep for recovery and adjust advice to their specific sleep patterns (use exact values from input if available, otherwise "unknown").

                By following all the above instructions, you will generate a comprehensive, personalized weekly exercise plan in JSON format. The plan should be safe, effective, and tailored to a breast cancer survivor’s needs, making full use of the provided clinical and wearable data. Remember: output only the JSON object with no extra commentary.
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
