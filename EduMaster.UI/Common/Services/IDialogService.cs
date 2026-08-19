using EduMaster.UI.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.UI.Common.Services
{
    public interface IDialogService
    {
        /// <summary>يعرض أي VM داخل قالب الديالوغ الموحّد — true إن أُغلق بحفظ/تأكيد</summary>
        Task<bool> ShowDialogAsync(BaseViewModel viewModel, string title);

        /// <summary>ديالوغ تأكيد جاهز للإجراءات المؤثرة</summary>
        Task<bool> ConfirmAsync(string title, string message, string confirmText = "تأكيد");
    }
}
