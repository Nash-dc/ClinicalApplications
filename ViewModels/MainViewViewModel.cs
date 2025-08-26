using ClinicalApplications.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Input;
using System.Linq;

namespace ClinicalApplications.ViewModels
{
    public partial class MainViewViewModel : ViewModelBase
    {
        private readonly GPTRequests _gptRequests;
        private readonly CtrcdRiskClient _riskClient;

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
        private Patient? _selectedPatient;
        public Patient? SelectedPatient
        {
            get => _selectedPatient;
            set => SetProperty(ref _selectedPatient, value);
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
                TestRiskClient(p);
                StatusMessage = $"Loaded patient from CSV: Age={p.Age}, LVEF={p.LVEF}";
            }
            catch (Exception ex)
            {
                StatusMessage = "CSV parse error: " + ex.Message;
            }
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

        protected bool SetProperty<T>(ref T storage, T value, string propertyName = null)
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
