using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.Application.AcademicYears.UpdateAcademicYear;
using EduMaster.Application.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.UI.AcademicYears
{
    public sealed class AcademicYearEditorViewModel : BaseViewModel, IDialogViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUserNotifier _notifier;

        public AcademicYearEditorViewModel(IServiceScopeFactory scopeFactory, IUserNotifier notifier)
        {
            _scopeFactory = scopeFactory;
            _notifier = notifier;

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
            CancelCommand = new AsyncRelayCommand(() =>
            {
                CloseRequested?.Invoke(this, false);
                return Task.CompletedTask;
            });
        }

        public event EventHandler<bool>? CloseRequested;

        private int? _editingId;   // null = إنشاء

        public string Title => _editingId is null ? "سنة دراسية جديدة" : "تعديل السنة الدراسية";

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                SetProperty(ref _startDate, value);
                OnPropertyChanged(nameof(DerivedName));
                OnPropertyChanged(nameof(HasValidRange));
            }
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                SetProperty(ref _endDate, value);
                OnPropertyChanged(nameof(DerivedName));
                OnPropertyChanged(nameof(HasValidRange));
            }
        }

        // 💡 الاسم يُشتق من التاريخين ويُعرض معاينةً حية — لا حقل اسم يدوي (قرار الشريحة)
        public string DerivedName => $"{StartDate.Year}-{EndDate.Year}";

        public bool HasValidRange => EndDate > StartDate;

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                SetProperty(ref _isSaving, value);
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public void InitializeForCreate()
        {
            _editingId = null;
            var today = DateTime.Today;
            StartDate = new DateTime(today.Year, 9, 1);        // افتراضي: سبتمبر
            EndDate = new DateTime(today.Year + 1, 7, 1);      // إلى جويلية التالية
        }

        public void InitializeForEdit(AcademicYearListItem year)
        {
            _editingId = year.Id;
            StartDate = year.StartDate.ToDateTime(TimeOnly.MinValue);
            EndDate = year.EndDate.ToDateTime(TimeOnly.MinValue);
        }

        private async Task SaveAsync()
        {
            ErrorMessage = null;

            if (!HasValidRange)
            {
                ErrorMessage = "تاريخ النهاية يجب أن يكون بعد تاريخ البداية.";
                return;
            }

            IsSaving = true;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var start = DateOnly.FromDateTime(StartDate);
                var end = DateOnly.FromDateTime(EndDate);

                if (_editingId is null)
                {
                    var handler = scope.ServiceProvider.GetRequiredService<CreateAcademicYearHandler>();
                    var result = await handler.ExecuteAsync(new CreateAcademicYearRequest(DerivedName, start, end));

                    if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "أُنشئت السنة الدراسية بنجاح ✔"))
                        return;
                }
                else
                {
                    var handler = scope.ServiceProvider.GetRequiredService<UpdateAcademicYearHandler>();
                    var result = await handler.ExecuteAsync(new UpdateAcademicYearRequest(_editingId.Value, DerivedName, start, end));

                    if (!HandleSaveResult(result.IsSuccess, result.ErrorMessage, result.ErrorType, "حُفظت التعديلات بنجاح ✔"))
                        return;
                }

                CloseRequested?.Invoke(this, true);
            }
            finally
            {
                IsSaving = false;
            }
        }

        // D-22 داخل الديالوغ: المتوقع ← بانر أحمر داخلي · غير المتوقع ← Toast
        private bool HandleSaveResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
        {
            if (isSuccess)
            {
                _notifier.ShowSuccess(successMessage);
                return true;
            }

            if (errorType == ErrorType.Unexpected)
                _notifier.ShowError(errorMessage!);
            else
                ErrorMessage = errorMessage;

            return false;
        }
    }
}
