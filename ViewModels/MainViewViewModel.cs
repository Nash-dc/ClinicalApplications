using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ClinicalApplications.Models;

namespace ClinicalApplications.ViewModels
{
    public partial class MainViewViewModel : ViewModelBase
    {
        private readonly GPTRequests _gptRequests;

        private string _prompt = "Generate a simple weekly walking plan for a breast cancer survivor after surgery.";
        public string Prompt
        {
            get => _prompt;
            set => SetProperty(ref _prompt, value);
        }

        private string _reply;
        public string Reply
        {
            get => _reply;
            set => SetProperty(ref _reply, value);
        }

        public ICommand SendRequestCommand { get; }

        public MainViewViewModel()
        {

            _gptRequests = new GPTRequests("gpt-5");

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
            TestRiskClient();
        }
        private async void TestRiskClient()
        {
            var client = new ClinicalApplications.Models.CtrcdRiskClient();
            var patient = new ClinicalApplications.Models.Patient
            {
                Age = 58,
                Weight = 70,
                Height = 165,
                LVEF = 55,
                HeartRate = 72,
                HeartRhythm = 0,
                PWT = 1.1,
                LAd = 3.8,
                LVDd = 4.8,
                LVSd = 3.1,
                AC = 1,
                AntiHER2 = 1,
                ACprev = 0,
                AntiHER2prev = 0,
                HTA = 1,
                DL = 0,
                DM = 0,
                Smoker = 0,
                ExSmoker = 1,
                RTprev = 0,
                CIprev = 0,
                ICMprev = 0,
                ARRprev = 0,
                VALVprev = 0,
                Cxvalv = 0
            };

            var res = await client.PredictAsync(patient, threshold: 0.28);
            Reply = $"Probability: {res.Prob}, Probability: {res.Pred}, Threshold: {res.Threshold}";

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
