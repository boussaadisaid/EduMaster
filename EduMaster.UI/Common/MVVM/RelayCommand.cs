using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EduMaster.UI.Common.MVVM
{
    public class RelayCommand : ICommand 
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null) 
        {
            _execute = execute;
            _canExecute = canExecute;
        }


        public event EventHandler? CanExecuteChanged;


        public void RaiseCanExecuteChanged() 
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }


        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }


        public void Execute(object? parameter) 
        {
            _execute(); 
        }
    }



    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();

                await _executeAsync();
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}