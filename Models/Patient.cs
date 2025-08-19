using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClinicalApplications.Models
{
    /// <summary>
    /// Represents a breast cancer patient with clinical features
    /// used for cardiotoxicity risk prediction.
    /// </summary>
    public sealed class Patient
    {
        /// <summary>
        /// Age of the patient in years.
        /// Older age is often associated with higher cardiotoxicity risk.
        /// </summary>
        [JsonPropertyName("age")] public int Age { get; set; }

        /// <summary>
        /// Body weight in kilograms.
        /// Used for BMI calculation and metabolic health evaluation.
        /// </summary>
        [JsonPropertyName("weight")] public double Weight { get; set; }

        /// <summary>
        /// Height in centimeters.
        /// Used together with weight to compute BMI.
        /// </summary>
        [JsonPropertyName("height")] public double Height { get; set; }

        /// <summary>
        /// Left Ventricular Ejection Fraction (in %).
        /// A primary indicator of cardiac function; low values suggest heart dysfunction.
        /// </summary>
        [JsonPropertyName("LVEF")] public double LVEF { get; set; }

        /// <summary>
        /// Heart rate in beats per minute.
        /// Tachycardia or bradycardia may indicate cardiac stress.
        /// </summary>
        [JsonPropertyName("heart_rate")] public int HeartRate { get; set; }

        /// <summary>
        /// Heart rhythm (0 = sinus rhythm, 1 = atrial fibrillation).
        /// Atrial fibrillation increases risk of cardiotoxicity complications.
        /// </summary>
        [JsonPropertyName("heart_rhythm")] public int HeartRhythm { get; set; }

        /// <summary>
        /// Posterior Wall Thickness of the left ventricle (in cm).
        /// Hypertrophy may reflect chronic cardiac stress.
        /// </summary>
        [JsonPropertyName("PWT")] public double PWT { get; set; }

        /// <summary>
        /// Left atrial diameter (in cm).
        /// Enlargement suggests chronic pressure overload.
        /// </summary>
        [JsonPropertyName("LAd")] public double LAd { get; set; }

        /// <summary>
        /// Left ventricular diastolic diameter (in cm).
        /// Indicates heart chamber size during relaxation.
        /// </summary>
        [JsonPropertyName("LVDd")] public double LVDd { get; set; }

        /// <summary>
        /// Left ventricular systolic diameter (in cm).
        /// Larger diameters may reflect impaired contractility.
        /// </summary>
        [JsonPropertyName("LVSd")] public double LVSd { get; set; }

        /// <summary>
        /// Current anthracycline chemotherapy (0 = no, 1 = yes).
        /// Anthracyclines are strongly cardiotoxic.
        /// </summary>
        [JsonPropertyName("AC")] public int AC { get; set; }

        /// <summary>
        /// Current anti-HER2 therapy (0 = no, 1 = yes).
        /// Anti-HER2 drugs (e.g., trastuzumab) are linked to heart dysfunction.
        /// </summary>
        [JsonPropertyName("antiHER2")] public int AntiHER2 { get; set; }

        /// <summary>
        /// History of anthracycline exposure (0 = no, 1 = yes).
        /// Previous use increases cumulative risk.
        /// </summary>
        [JsonPropertyName("ACprev")] public int ACprev { get; set; }

        /// <summary>
        /// History of anti-HER2 therapy (0 = no, 1 = yes).
        /// Past treatment may leave residual cardiac injury.
        /// </summary>
        [JsonPropertyName("antiHER2prev")] public int AntiHER2prev { get; set; }

        /// <summary>
        /// Hypertension (0 = no, 1 = yes).
        /// A known comorbidity increasing cardiac vulnerability.
        /// </summary>
        [JsonPropertyName("HTA")] public int HTA { get; set; }

        /// <summary>
        /// Dyslipidemia (0 = no, 1 = yes).
        /// Contributes to atherosclerosis and cardiac risk.
        /// </summary>
        [JsonPropertyName("DL")] public int DL { get; set; }

        /// <summary>
        /// Diabetes mellitus (0 = no, 1 = yes).
        /// A strong cardiovascular risk factor.
        /// </summary>
        [JsonPropertyName("DM")] public int DM { get; set; }

        /// <summary>
        /// Current smoking status (0 = no, 1 = yes).
        /// Smoking damages vascular and cardiac function.
        /// </summary>
        [JsonPropertyName("smoker")] public int Smoker { get; set; }

        /// <summary>
        /// Former smoker status (0 = no, 1 = yes).
        /// Residual risk remains even after quitting.
        /// </summary>
        [JsonPropertyName("exsmoker")] public int ExSmoker { get; set; }

        /// <summary>
        /// Previous thoracic radiotherapy (0 = no, 1 = yes).
        /// Can cause radiation-induced cardiotoxicity.
        /// </summary>
        [JsonPropertyName("RTprev")] public int RTprev { get; set; }

        /// <summary>
        /// Previous cardiac insufficiency (heart failure) (0 = no, 1 = yes).
        /// Strong predictor of further cardiac decline.
        /// </summary>
        [JsonPropertyName("CIprev")] public int CIprev { get; set; }

        /// <summary>
        /// Previous ischemic cardiomyopathy (0 = no, 1 = yes).
        /// Indicates pre-existing coronary artery disease.
        /// </summary>
        [JsonPropertyName("ICMprev")] public int ICMprev { get; set; }

        /// <summary>
        /// Previous arrhythmia (0 = no, 1 = yes).
        /// Electrical instability increases risk under treatment.
        /// </summary>
        [JsonPropertyName("ARRprev")] public int ARRprev { get; set; }

        /// <summary>
        /// Previous valvulopathy (0 = no, 1 = yes).
        /// Valve disease can worsen chemotherapy-induced dysfunction.
        /// </summary>
        [JsonPropertyName("VALVprev")] public int VALVprev { get; set; }

        /// <summary>
        /// Previous valve surgery (0 = no, 1 = yes).
        /// Reflects significant prior cardiac intervention.
        /// </summary>
        [JsonPropertyName("cxvalv")] public int Cxvalv { get; set; }

        /// <summary>
        /// Convert this patient into a dictionary for JSON serialization.
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["age"] = Age,
                ["weight"] = Weight,
                ["height"] = Height,
                ["LVEF"] = LVEF,
                ["heart_rate"] = HeartRate,
                ["heart_rhythm"] = HeartRhythm,
                ["PWT"] = PWT,
                ["LAd"] = LAd,
                ["LVDd"] = LVDd,
                ["LVSd"] = LVSd,
                ["AC"] = AC,
                ["antiHER2"] = AntiHER2,
                ["ACprev"] = ACprev,
                ["antiHER2prev"] = AntiHER2prev,
                ["HTA"] = HTA,
                ["DL"] = DL,
                ["DM"] = DM,
                ["smoker"] = Smoker,
                ["exsmoker"] = ExSmoker,
                ["RTprev"] = RTprev,
                ["CIprev"] = CIprev,
                ["ICMprev"] = ICMprev,
                ["ARRprev"] = ARRprev,
                ["VALVprev"] = VALVprev,
                ["cxvalv"] = Cxvalv
            };
        }
    }
}
