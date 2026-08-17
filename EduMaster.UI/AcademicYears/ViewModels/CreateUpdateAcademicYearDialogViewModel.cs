using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.UI.Common.MVVM;
using System.Text.RegularExpressions;



namespace EduMaster.UI.AcademicYears.ViewModels
{
    public class CreateUpdateAcademicYearDialogViewModel : BaseViewModel
    {
        private readonly CreateAcademicYearHandler _createAcademicYearHandler;
        //============================
        // Private fields
        //============================

        private string _title = "إضافة سنة دراسية";
        private string _name = string.Empty;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddMonths(12);

        //============================
        // Public properties
        //============================

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }


        public string Name
        {
            get => _name;
            set
            {
                if(SetProperty<string>(ref _name, value))
                {
                    ValidateName();
                    ValidateDates();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty<DateTime>(ref _startDate, value))
                {
                    ValidateDates();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty<DateTime>(ref _endDate, value))
                {
                    ValidateDates();
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        //============================
        // Commands
        //============================
        public AsyncRelayCommand SaveCommand {  get; }
        public RelayCommand CancelCommand { get; }


        // ================================
        // 4) Constructor
        // ================================
        public CreateUpdateAcademicYearDialogViewModel(CreateAcademicYearHandler createAcademicYearHandler)
        {
            _createAcademicYearHandler = createAcademicYearHandler;

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new RelayCommand(Cancel);
        }

        // ================================
        // 6) Command Logic
        // ================================
        private async Task SaveAsync()
        {
            try
            {
                var startDate = DateOnly.FromDateTime(StartDate);
                var endDate = DateOnly.FromDateTime(EndDate);

                var createAcademicYearCommand = new CreateAcademicYearCommand(Name, startDate, endDate);

                var createAcademicYearResult = await _createAcademicYearHandler.Handle(createAcademicYearCommand);
                // لاحقًا: إغلاق النافذة أو إشعار النجاح
            }
            catch (Exception ex) 
            {
                // لاحقًا: إظهار الخطأ في الواجهة
            }
            
        }

        private bool CanSave()
        {
            ValidateName();
            ValidateDates();

            return !HasErrors;            
        }

        private void Cancel()
        {
            // لاحقا
        }

        // ================================
        // 5) Validation
        // ================================
        private void ValidateName()
        {
            ClearErrors(nameof(Name));  

            if (string.IsNullOrWhiteSpace(Name))
                AddError(nameof(Name), "السنة الدراسية مطلوبة");

            if (!Regex.IsMatch(Name, @"^(20\d{2})-(20\d{2})$"))
                AddError(nameof(Name), "الصيغة المطلوبة مثل: 2025-2026");
        }

        private void ValidateDates()
        {
            ClearErrors(nameof(StartDate));
            ClearErrors(nameof(EndDate));

            if (StartDate >= EndDate)
            {
                AddError(nameof(StartDate), "تاريخ بداية السنة يجب ان يكون اقل من تاريخ نهايتها");
                AddError(nameof(EndDate), "تاريخ نهاية السنة يجب ان يكون اكبر من تاريخ بدايتها");
            }

            if (string.IsNullOrWhiteSpace(Name) || !Regex.IsMatch(Name, @"^(20\d{2})-(20\d{2})$"))
                return;

            var parts = Name.Split('-');

            if (parts[0] != StartDate.Year.ToString())
                AddError(nameof(StartDate), "تاريخ البداية ليس ضمن السنة الدراسية");

            if(parts[1] != EndDate.Year.ToString())
                AddError(nameof(EndDate), "تاريخ النهاية ليس ضمن السنة الدراسية");
        }
    }
}
