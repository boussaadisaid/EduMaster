using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.UI.Common.MVVM
{
    /// <summary>عقد أي VM يُعرض داخل ديالوغ — يرفع CloseRequested بالنتيجة (true = حفظ/تأكيد)</summary>
    public interface IDialogViewModel
    {
        event EventHandler<bool>? CloseRequested;
    }
}
