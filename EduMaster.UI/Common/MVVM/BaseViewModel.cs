using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace EduMaster.UI.Common.MVVM
{
    public class BaseViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        

        //===============================
        //INotifyPropertyChanged
        //===============================

        public event PropertyChangedEventHandler? PropertyChanged;
       

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if(EqualityComparer<T>.Default.Equals(field, value))
                return false;


            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        //==============================
        // INotifyDataErrorInfo
        //==============================

        private readonly Dictionary<string, List<string>> _errors = new();

        public bool HasErrors => _errors.Any();

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if(string.IsNullOrWhiteSpace(propertyName))
                return new List<string>();

            return _errors.ContainsKey(propertyName) ? _errors[propertyName] : new List<string>();
        }

        protected void AddError(string propertyName, string errorMessage)
        {
            if (!_errors.ContainsKey(propertyName))
                _errors.Add(propertyName, new List<string>());

            if (!_errors[propertyName].Contains(errorMessage))
            {
                _errors[propertyName].Add(errorMessage);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                OnPropertyChanged(nameof(HasErrors));
            }
                
        }

        public void ClearErrors(string propertyName)
        {
            if (_errors.Remove(propertyName))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                OnPropertyChanged(nameof(HasErrors));
            }
                
        }


    }
}
