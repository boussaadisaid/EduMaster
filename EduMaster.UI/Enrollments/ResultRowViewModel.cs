using EduMaster.Application.Enrollments;

namespace EduMaster.UI.Enrollments;

/// <summary>سطر تقرير التنفيذ — الاسم مُدمج من قائمة المرشحين عند العرض (روح D-128: لا نص من مصدر ثانٍ)</summary>
public sealed class ResultRowViewModel
{
    public ResultRowViewModel(string fullName, RolloverStudentResult result)
    {
        FullName = fullName;
        OutcomeText = result.OutcomeText;
        Reason = result.Reason ?? "—";
        IsSuccess = result.Outcome == RolloverOutcome.Success;
        IsFailed = result.Outcome == RolloverOutcome.Failed;
    }

    public string FullName { get; }
    public string OutcomeText { get; }
    public string Reason { get; }
    public bool IsSuccess { get; }
    public bool IsFailed { get; }
}