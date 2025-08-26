using ClinicalApplications.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClinicalApplications.ViewModels
{
    public partial class MainViewViewModel : ViewModelBase
    {
        private readonly GPTRequests _gptRequests;
        private readonly CtrcdRiskClient _riskClient;
        public ObservableCollection<DisplayField> PatientDisplay { get; } = new();
        private Patient? _selectedPatient;
        public Patient? SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                if (SetProperty(ref _selectedPatient, value))
                {
                    UpdatePatientDisplay();
                }
            }
        }
        private static string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private string _prompt = "Generate a simple weekly walking plan for a breast cancer survivor after surgery.";
        public string Prompt
        {
            get => _prompt;
            set => SetProperty(ref _prompt, value);
        }

        private string? _reply;
        public string? Reply
        {
            get => _reply;
            set => SetProperty(ref _reply, value);
        }
        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SendRequestCommand { get; }

        public MainViewViewModel()
        {

            _gptRequests = new GPTRequests("gpt-5");
            _riskClient = new CtrcdRiskClient();

            SendRequestCommand = new AsyncRelayCommand(async () =>
            {
                try
                {
                    Reply = "Sending...";
                    Reply = await _gptRequests.AskAsync(Prompt);
                }
                catch (Exception ex)
                {
                    Reply = "Error: " + ex.Message;
                }
            });
        }
        private async void TestRiskClient(Patient patient)
        {
            var res = await _riskClient.PredictAsync(patient, threshold: 0.28);
            Reply = $"Probability: {res.Prob}, Probability: {res.Pred}, Threshold: {res.Threshold}";
        }

        public async Task LoadPatientFromCsvAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    StatusMessage = "File not found.";
                    return;
                }

                var lines = await File.ReadAllLinesAsync(path);
                if (lines.Length < 2)
                {
                    StatusMessage = "CSV has no data rows.";
                    return;
                }

                char sep = lines[0].Contains(';') && !lines[0].Contains(',') ? ';' : ',';

                var headers = lines[0].Split(sep).Select(h => h.Trim()).ToArray();
                var values = lines[1].Split(sep).Select(v => v.Trim()).ToArray();

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < Math.Min(headers.Length, values.Length); i++)
                    dict[headers[i]] = values[i];

                var p = new Patient
                {
                    Age = GetInt(dict, "age"),
                    Weight = GetDouble(dict, "weight"),
                    Height = GetDouble(dict, "height"),
                    LVEF = GetDouble(dict, "LVEF"),
                    HeartRate = GetInt(dict, "heart_rate"),
                    HeartRhythm = GetInt(dict, "heart_rhythm"),
                    PWT = GetDouble(dict, "PWT"),
                    LAd = GetDouble(dict, "LAd"),
                    LVDd = GetDouble(dict, "LVDd"),
                    LVSd = GetDouble(dict, "LVSd"),
                    AC = GetInt(dict, "AC"),
                    AntiHER2 = GetInt(dict, "antiHER2"),
                    ACprev = GetInt(dict, "ACprev"),
                    AntiHER2prev = GetInt(dict, "antiHER2prev"),
                    HTA = GetInt(dict, "HTA"),
                    DL = GetInt(dict, "DL"),
                    DM = GetInt(dict, "DM"),
                    Smoker = GetInt(dict, "smoker"),
                    ExSmoker = GetInt(dict, "exsmoker"),
                    RTprev = GetInt(dict, "RTprev"),
                    CIprev = GetInt(dict, "CIprev"),
                    ICMprev = GetInt(dict, "ICMprev"),
                    ARRprev = GetInt(dict, "ARRprev"),
                    VALVprev = GetInt(dict, "VALVprev"),
                    Cxvalv = GetInt(dict, "cxvalv")
                };

                SelectedPatient = p;
                UpdatePatientDisplay();
                StatusMessage = $"Loaded patient from CSV: Age={p.Age}, LVEF={p.LVEF}";
            }
            catch (Exception ex)
            {
                StatusMessage = "CSV parse error: " + ex.Message;
            }
        }
        public void UpdatePatientDisplay()
        {
            PatientDisplay.Clear();
            var p = SelectedPatient;
            if (p is null) return;

            void Add(string label, string val, string tip)
                => PatientDisplay.Add(new DisplayField { Label = label, Value = val, Tooltip = tip });

            Add("Age (years)", I(p.Age), "Patient age in years. Higher age is associated with higher cardiotoxicity risk.");
            Add("Weight (kg)", F(p.Weight), "Body weight in kilograms. Used to calculate BMI and assess metabolic health.");
            Add("Height (cm)", F(p.Height), "Body height in centimeters. Used with weight to compute BMI.");
            Add("LVEF (%)", F(p.LVEF), "Left Ventricular Ejection Fraction. Lower values indicate impaired cardiac function.");
            Add("Heart rate (bpm)", I(p.HeartRate), "Resting heart rate (beats per minute). Tachycardia/bradycardia may signal stress.");
            Add("Heart rhythm (0/1)", I(p.HeartRhythm), "0 = sinus rhythm; 1 = atrial fibrillation. AF increases complications risk.");
            Add("PWT (cm)", F(p.PWT), "Posterior Wall Thickness of LV. Hypertrophy may reflect chronic stress.");
            Add("LAd (cm)", F(p.LAd), "Left Atrial diameter. Enlargement suggests chronic pressure/volume overload.");
            Add("LVDd (cm)", F(p.LVDd), "Left Ventricular Diastolic diameter. Chamber size during relaxation/diastole.");
            Add("LVSd (cm)", F(p.LVSd), "Left Ventricular Systolic diameter. Larger values may reflect poor contractility.");
            Add("AC (0/1)", I(p.AC), "Current anthracycline therapy. Anthracyclines are strongly cardiotoxic.");
            Add("antiHER2 (0/1)", I(p.AntiHER2), "Current anti-HER2 therapy (e.g., trastuzumab). Associated with LV dysfunction.");
            Add("ACprev (0/1)", I(p.ACprev), "History of anthracycline exposure. Cumulative exposure increases risk.");
            Add("antiHER2prev (0/1)", I(p.AntiHER2prev), "History of anti-HER2 therapy. Past exposure may leave residual injury.");
            Add("HTA (0/1)", I(p.HTA), "Hypertension. A known comorbidity increasing vulnerability.");
            Add("DL (0/1)", I(p.DL), "Dyslipidemia. Contributes to atherosclerosis and cardiac risk.");
            Add("DM (0/1)", I(p.DM), "Diabetes mellitus. Strong risk factor for cardiovascular disease.");
            Add("Smoker (0/1)", I(p.Smoker), "Current smoking status. Harms vascular and cardiac function.");
            Add("Ex-smoker (0/1)", I(p.ExSmoker), "Former smoker status. Residual risk may remain.");
            Add("RTprev (0/1)", I(p.RTprev), "Previous thoracic radiotherapy. Radiation can induce cardiotoxicity.");
            Add("CIprev (0/1)", I(p.CIprev), "Previous cardiac insufficiency (heart failure). Strong predictor of decline.");
            Add("ICMprev (0/1)", I(p.ICMprev), "Previous ischemic cardiomyopathy. Indicates established coronary disease.");
            Add("ARRprev (0/1)", I(p.ARRprev), "Previous arrhythmia. Electrical instability under therapy.");
            Add("VALVprev (0/1)", I(p.VALVprev), "Previous valvulopathy. Valve disease worsens chemo-related dysfunction.");
            Add("cxvalv (0/1)", I(p.Cxvalv), "Previous valve surgery. Reflects significant prior cardiac intervention.");
        }
        private static int GetInt(Dictionary<string, string> d, string key, int def = 0)
        {
            if (d.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            return def;
        }

        private static double GetDouble(Dictionary<string, string> d, string key, double def = 0)
        {
            if (d.TryGetValue(key, out var s))
            {
                s = s.Replace(',', '.');
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            return def;
        }

        protected bool SetProperty<T>(ref T storage, T value, string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);
        public event EventHandler CanExecuteChanged;

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;
            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try { await _execute(); }
            finally { _isExecuting = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
        }
    }
}
