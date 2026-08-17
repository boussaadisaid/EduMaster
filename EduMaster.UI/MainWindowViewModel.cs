using EduMaster.UI.AcademicYears.Services;
using EduMaster.UI.Common.MVVM;




namespace EduMaster.UI
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IAcademicYearDialogService _academicYearDialogService;


        

       


        public RelayCommand OpenCreateAcademicYearDialogCommand { get;}

        public MainWindowViewModel(/*IAcademicYearDialogService academicYearDialogService*/)
        {
            OpenCreateAcademicYearDialogCommand = new RelayCommand(OpenCreateAcademicYearDialog);
            //_academicYearDialogService = academicYearDialogService;
        }

        private void OpenCreateAcademicYearDialog()
        {
           
            //_academicYearDialogService.ShowCreateAcademicYearDialog();
        }
    }
}
