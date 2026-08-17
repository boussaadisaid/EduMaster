using EduMaster.Application.AcademicYears.CreateAcademicYear;
using EduMaster.UI.AcademicYears.Dialogs;
using EduMaster.UI.AcademicYears.ViewModels;



namespace EduMaster.UI.AcademicYears.Services
{
    public sealed class AcademicYearDialogService : IAcademicYearDialogService
    {
        private readonly CreateAcademicYearHandler _createAcademicYearHandler;

        public AcademicYearDialogService(CreateAcademicYearHandler createAcademicYearHandler)
        {
            _createAcademicYearHandler = createAcademicYearHandler;
        }

        public bool? ShowCreateAcademicYearDialog()
        {
            var viewModel = new CreateUpdateAcademicYearDialogViewModel(_createAcademicYearHandler);

            var dialog = new CreateUpdateAcademicYearDialog(viewModel);


            return dialog.ShowDialog();
        }
    }

}
