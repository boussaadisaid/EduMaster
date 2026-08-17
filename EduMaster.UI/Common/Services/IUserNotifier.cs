

namespace EduMaster.UI.Common.Services
{
    public interface IUserNotifier
    {
        void ShowSuccess(string message);
        void ShowError(string message);
        void ShowWarning(string message); // تمت إضافتها لشمولية الحالات
        void ShowInfo(string message);
    }
}
