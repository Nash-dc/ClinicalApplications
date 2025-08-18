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
