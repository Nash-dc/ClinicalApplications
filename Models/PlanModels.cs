using System.Collections.Generic;

namespace ClinicalApplications.Models
{
    public sealed class DayPlan
    {
        public string day { get; set; } = "";     // "Mon"..."Sun"
        public int steps { get; set; }            // 0..MaxDailySteps
        public int active_minutes { get; set; }   // 0..MaxDailyActiveMinutes
        public string rpe { get; set; } = "";     // "1-3"
    }

    public sealed class PlanJson
    {
        public string version { get; set; } = "1.0";
        public List<DayPlan> week { get; set; } = new();
        public List<string> safety_notes { get; set; } = new();
        public string pause_rule { get; set; } = "";

        // Extended note fields
        public List<string> weight_notes { get; set; } = new();
        public List<string> upper_limb_notes { get; set; } = new();
        public List<string> psychological_notes { get; set; } = new();
        public List<string> sleep_notes { get; set; } = new();
    }
}
