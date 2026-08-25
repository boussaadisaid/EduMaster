using EduMaster.UI.Common.MVVM;

namespace EduMaster.UI.Billing;

/// <summary>صف مستحق مفتوح في ديالوغ القبض — «يُخصَّص» قابل للتعديل فوق الاقتراح التلقائي (D-106)</summary>
public sealed class PaymentAllocationRowViewModel : BaseViewModel
{
    private readonly Action _onChanged;

    public PaymentAllocationRowViewModel(int chargeId, string kindText, string sourceText, string remainingText, Action onChanged)
    {
        ChargeId = chargeId;
        KindText = kindText;
        SourceText = sourceText;
        RemainingText = remainingText;
        _allocatedText = string.Empty;
        _onChanged = onChanged;
    }

    public int ChargeId { get; }
    public string KindText { get; }
    public string SourceText { get; }
    public string RemainingText { get; }

    private string _allocatedText;
    public string AllocatedText
    {
        get => _allocatedText;
        set
        {
            if (SetProperty(ref _allocatedText, value))
                _onChanged();   // سطر «غير مخصص من هذه الدفعة» حيّ
        }
    }
}