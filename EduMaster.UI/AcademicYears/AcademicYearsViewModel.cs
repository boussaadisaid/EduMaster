using EduMaster.Application.AcademicYears;
using EduMaster.Application.AcademicYears.ActivateAcademicYear;
using EduMaster.Application.AcademicYears.DeactivateAcademicYear;
using EduMaster.Application.AcademicYears.SetCurrentAcademicYear;
using EduMaster.Application.Common;
using EduMaster.UI.Common.MVVM;
using EduMaster.UI.Common.Services;
using EduMaster.UI.Enrollments;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.UI.AcademicYears
{
    public sealed class AcademicYearsViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceProvider _services;
        private readonly IUserNotifier _notifier;
        private readonly IDialogService _dialogs;

        public AcademicYearsViewModel(
            IServiceScopeFactory scopeFactory,
            IServiceProvider services,
            IUserNotifier notifier,
            IDialogService dialogs)
        {
            _scopeFactory = scopeFactory;
            _services = services;
            _notifier = notifier;
            _dialogs = dialogs;

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            AddCommand = new AsyncRelayCommand(AddAsync);
            EditCommand = new AsyncRelayCommand(EditAsync, () => SelectedYear is not null);
            SetCurrentCommand = new AsyncRelayCommand(SetCurrentAsync, () => SelectedYear is { IsActive: true, IsCurrent: false });
            DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => SelectedYear is { IsActive: true, IsCurrent: false });
            ActivateCommand = new AsyncRelayCommand(ActivateAsync, () => SelectedYear is { IsActive: false });
            // F6 — الشريحة 6.2: الترحيل الجماعي إلى السنة المحددة هدفاً (D-129) — الهدف فعّال إلزاماً (يحرسه الـHandler أيضاً)
            RolloverCommand = new AsyncRelayCommand(RolloverAsync, () => SelectedYear is { IsActive: true });
        }

        // ---------- الحالة ----------

        public ObservableCollection<AcademicYearListItem> Years { get; } = new();

        private AcademicYearListItem? _selectedYear;
        public AcademicYearListItem? SelectedYear
        {
            get => _selectedYear;
            set
            {
                SetProperty(ref _selectedYear, value);
                EditCommand.RaiseCanExecuteChanged();
                SetCurrentCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
                ActivateCommand.RaiseCanExecuteChanged();
                RolloverCommand.RaiseCanExecuteChanged();
            }
        }

        private AcademicYearListItem? _currentYear;
        public AcademicYearListItem? CurrentYear
        {
            get => _currentYear;
            private set
            {
                SetProperty(ref _currentYear, value);
                OnPropertyChanged(nameof(CurrentYearName));
            }
        }

        public string CurrentYearName => CurrentYear?.Name ?? "لا توجد سنة حالية بعد";

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                SetProperty(ref _isLoading, value);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        public bool IsEmpty => !IsLoading && Years.Count == 0;

        // ---------- الأوامر ----------

        public AsyncRelayCommand RefreshCommand { get; }
        public AsyncRelayCommand AddCommand { get; }
        public AsyncRelayCommand EditCommand { get; }
        public AsyncRelayCommand SetCurrentCommand { get; }
        public AsyncRelayCommand DeactivateCommand { get; }
        public AsyncRelayCommand ActivateCommand { get; }
        public AsyncRelayCommand RolloverCommand { get; }   // جديد 6.2-ج

        public Task InitializeAsync() => LoadAsync();

        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();   // Scope-per-Use-Case
                var handler = scope.ServiceProvider.GetRequiredService<GetAllAcademicYearsHandler>();
                var result = await handler.ExecuteAsync();

                if (result.IsSuccess)
                {
                    Years.Clear();
                    foreach (var year in result.Value!)
                        Years.Add(year);

                    CurrentYear = Years.FirstOrDefault(y => y.IsCurrent);
                    SelectedYear = SelectedYear is null ? null : Years.FirstOrDefault(y => y.Id == SelectedYear.Id);
                }
                else
                {
                    _notifier.ShowError(result.ErrorMessage!);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddAsync()
        {
            var editor = _services.GetRequiredService<AcademicYearEditorViewModel>();
            editor.InitializeForCreate();

            if (await _dialogs.ShowDialogAsync(editor, editor.Title))
                await LoadAsync();
        }

        private async Task EditAsync()
        {
            if (SelectedYear is null) return;

            var editor = _services.GetRequiredService<AcademicYearEditorViewModel>();
            editor.InitializeForEdit(SelectedYear);

            if (await _dialogs.ShowDialogAsync(editor, editor.Title))
                await LoadAsync();
        }

        private async Task SetCurrentAsync()
        {
            if (SelectedYear is null) return;

            var message = CurrentYear is null
                ? $"ستصبح «{SelectedYear.Name}» هي السنة الحالية."
                : $"ستصبح «{SelectedYear.Name}» هي السنة الحالية، وستُلغى حالية «{CurrentYear.Name}» تلقائياً.";

            if (!await _dialogs.ConfirmAsync("تعيين السنة الحالية", message, "تعيين"))
                return;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<SetCurrentAcademicYearHandler>();
            var result = await handler.ExecuteAsync(new SetCurrentAcademicYearRequest(SelectedYear.Id));

            await HandleActionResult(result.IsSuccess, result.ErrorMessage, result.ErrorType,
                $"أصبحت «{SelectedYear.Name}» هي السنة الحالية ✔");
        }

        private async Task DeactivateAsync()
        {
            if (SelectedYear is null) return;

            var confirmed = await _dialogs.ConfirmAsync(
                "تعطيل السنة الدراسية",
                $"ستُعطَّل «{SelectedYear.Name}» وتُخفى من قوائم الاختيار مستقبلاً، دون حذف بياناتها. يمكن إعادة تفعيلها في أي وقت.",
                "تعطيل");

            if (!confirmed) return;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<DeactivateAcademicYearHandler>();
            var result = await handler.ExecuteAsync(new DeactivateAcademicYearRequest(SelectedYear.Id));

            await HandleActionResult(result.IsSuccess, result.ErrorMessage, result.ErrorType,
                $"عُطّلت «{SelectedYear.Name}»");
        }

        private async Task ActivateAsync()
        {
            if (SelectedYear is null) return;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<ActivateAcademicYearHandler>();
            var result = await handler.ExecuteAsync(new ActivateAcademicYearRequest(SelectedYear.Id));

            await HandleActionResult(result.IsSuccess, result.ErrorMessage, result.ErrorType,
                $"فُعّلت «{SelectedYear.Name}»");
        }

        private async Task RolloverAsync()
        {
            if (SelectedYear is null) return;

            var dialog = _services.GetRequiredService<RolloverDialogViewModel>();
            await dialog.InitializeAsync(SelectedYear);

            // النتيجة لا تغيّر شبكة السنوات — التسجيلات الجديدة تظهر في لوحات الطلاب وكشوفها
            await _dialogs.ShowDialogAsync(dialog, dialog.Title);
        }

        // D-22 على شاشة بلا فورم: النجاح ← Toast · المتوقع (قاعدة عمل) ← Toast تحذيري · غير المتوقع ← Toast خطأ
        private async Task HandleActionResult(bool isSuccess, string? errorMessage, ErrorType errorType, string successMessage)
        {
            if (isSuccess)
            {
                _notifier.ShowSuccess(successMessage);
                await LoadAsync();
            }
            else if (errorType == ErrorType.Unexpected)
            {
                _notifier.ShowError(errorMessage!);
            }
            else
            {
                _notifier.ShowWarning(errorMessage!);
            }
        }
    }
}