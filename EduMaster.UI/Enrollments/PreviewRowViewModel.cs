using EduMaster.Application.Enrollments;
using EduMaster.UI.Common.MVVM;

namespace EduMaster.UI.Enrollments;

/// <summary>سطر معاينة الترحيل (تر-5/تر-6): خانة اختيار للأهلّ فقط — الكل محدد افتراضياً · المستبعد والمسجَّل مسبقاً مرئيان بسببهما (روح D-124)</summary>
public sealed class PreviewRowViewModel : BaseViewModel
{
    private readonly Action _onSelectionChanged;

    public PreviewRowViewModel(RolloverCandidateItem candidate, Action onSelectionChanged)
    {
        Candidate = candidate;
        _onSelectionChanged = onSelectionChanged;
        _isSelected = CanSelect;
    }

    public RolloverCandidateItem Candidate { get; }

    public bool CanSelect => Candidate.IsEligible && !Candidate.AlreadyInTarget;

    public string SourceLabel => Candidate.SourceStreamName is null
        ? $"{Candidate.SourceLevelName} — بلا شعبة"
        : $"{Candidate.SourceLevelName} — {Candidate.SourceStreamName}";

    public string StatusText => !Candidate.IsEligible ? $"مستبعد: {Candidate.ExclusionReason}"
        : Candidate.AlreadyInTarget ? "في الهدف مسبقاً — سيُتخطّى"
        : "جاهز";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (CanSelect && SetProperty(ref _isSelected, value))
                _onSelectionChanged();
        }
    }

    private string _targetLabel = "—";
    public string TargetLabel
    {
        get => _targetLabel;
        set => SetProperty(ref _targetLabel, value);
    }
}