namespace EduMaster.Application.Enrollments;

/// <summary>مرشح ترحيل من سنة المصدر (6.2 — D-129/تر-5/تر-6): معاينة قبل التنفيذ — المستبعد يظهر بسببه (لا اختفاء صامت روح D-124) والدين معلوماتي لا مانع · قراءة مسطّحة (D-40)</summary>
public sealed record RolloverCandidateItem(
    int StudentId,
    string FullName,
    string? Phone,
    int SourceLevelId,
    string SourceLevelName,
    int? SourceStreamId,
    string? SourceStreamName,
    bool AlreadyInTarget,
    bool IsEligible,
    string? ExclusionReason,
    long DebtCentimes);

/// <summary>صف خريطة الانتقال (تر-3): (مستوى + شعبة مصدر) ← (مستوى + شعبة هدف) — الشعبة الفارغة تعني «بلا شعبة» (D-59/D-60)</summary>
public sealed record RolloverMappingInput(int SourceLevelId, int? SourceStreamId, int TargetLevelId, int? TargetStreamId);

/// <summary>طلب الترحيل الجماعي: السنتان صراحة (تر-2) + الخريطة + المحدَّدون من المعاينة</summary>
public sealed record BulkRolloverRequest(
    int SourceYearId,
    int TargetYearId,
    IReadOnlyList<RolloverMappingInput> Mappings,
    IReadOnlyList<int> StudentIds);

/// <summary>مصير طالب في الترحيل (تر-7)</summary>
public enum RolloverOutcome : byte { Success = 1, Skipped = 2, Failed = 3 }

/// <summary>سطر تقرير التنفيذ — الاسم يُدمج في الواجهة من قائمة المرشحين (المعرف يكفي هنا)</summary>
public sealed record RolloverStudentResult(int StudentId, RolloverOutcome Outcome, string? Reason)
{
    public string OutcomeText => Outcome switch
    {
        RolloverOutcome.Success => "✔ رُحّل",
        RolloverOutcome.Skipped => "⏭ تُخطّي",
        _ => "✖ فشل",
    };
}

/// <summary>تقرير الترحيل الجماعي — العدادات مشتقة من السطور</summary>
public sealed record BulkRolloverResultItem(IReadOnlyList<RolloverStudentResult> Rows)
{
    public int SuccessCount => Rows.Count(r => r.Outcome == RolloverOutcome.Success);
    public int SkippedCount => Rows.Count(r => r.Outcome == RolloverOutcome.Skipped);
    public int FailedCount => Rows.Count(r => r.Outcome == RolloverOutcome.Failed);
}