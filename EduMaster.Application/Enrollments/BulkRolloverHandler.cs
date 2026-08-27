using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// الترحيل الجماعي بين السنوات (6.2 — D-129): لكل طالب محدَّد يُعاد استعمال handler التسجيل الفردي كما هو
/// (معاملة مستقلة + مستحق الحقوق ذرّياً D-103 + سلسلة الحُراس كاملةً) — فشل صف لا يوقف الباقين، وإعادة الضغط آمنة (روح D-87).
/// الخريطة تُتحقق مقدماً دفعة واحدة (صف فاسد لا يُسقط عشرات الطلاب) · الحقوق = افتراضي السنة الهدف (تر-4/D-66) والإعفاءات لا تُنسخ.
/// لا معاملة خاصة به: لا كتابة مباشرة فيه — الذرّية لكل طالب عند الـhandler الفردي.
/// </summary>
public sealed class BulkRolloverHandler
{
    private readonly RegisterAnnualEnrollmentHandler _register;
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly IAcademicYearRepository _years;
    private readonly ILevelRepository _levels;
    private readonly IStreamRepository _streams;
    private readonly ILogger<BulkRolloverHandler> _logger;

    public BulkRolloverHandler(RegisterAnnualEnrollmentHandler register, IAnnualEnrollmentRepository enrollments,
        IAcademicYearRepository years, ILevelRepository levels, IStreamRepository streams,
        ILogger<BulkRolloverHandler> logger)
    {
        _register = register;
        _enrollments = enrollments;
        _years = years;
        _levels = levels;
        _streams = streams;
        _logger = logger;
    }

    public async Task<OperationResult<BulkRolloverResultItem>> ExecuteAsync(BulkRolloverRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceYearId == request.TargetYearId)
            return OperationResult<BulkRolloverResultItem>.Failure("سنة المصدر هي نفسها سنة الهدف — اختر سنتين مختلفتين.", ErrorType.Validation);
        if (request.Mappings.Count == 0)
            return OperationResult<BulkRolloverResultItem>.Failure("لا خريطة انتقال — عرّف صفاً واحداً على الأقل.", ErrorType.Validation);
        if (request.StudentIds.Count == 0)
            return OperationResult<BulkRolloverResultItem>.Failure("لا طلاب محدَّدون للترحيل — حدّدهم من المعاينة أولاً.", ErrorType.Validation);

        try
        {
            var targetYear = await _years.GetByIdAsync(request.TargetYearId, cancellationToken);
            if (targetYear is null)
                return OperationResult<BulkRolloverResultItem>.Failure("السنة الدراسية الهدف غير موجودة.", ErrorType.Validation);
            if (!targetYear.IsActive)
                return OperationResult<BulkRolloverResultItem>.Failure("لا يمكن الترحيل إلى سنة معطّلة — فعّلها من شاشة السنوات أولاً.", ErrorType.BusinessRule);

            // المصدر: الوجود يكفي — قد تكون معطّلة (سنة منتهية) وهذا طبيعي في الترحيل منها (تر-2)
            var sourceYear = await _years.GetByIdAsync(request.SourceYearId, cancellationToken);
            if (sourceYear is null)
                return OperationResult<BulkRolloverResultItem>.Failure("سنة المصدر غير موجودة.", ErrorType.Validation);

            // تكرار صف مصدر في الخريطة = غموض — يُرفض ودّياً قبل أي تنفيذ
            var hasDuplicateSource = request.Mappings
                .GroupBy(m => (m.SourceLevelId, m.SourceStreamId))
                .Any(g => g.Count() > 1);
            if (hasDuplicateSource)
                return OperationResult<BulkRolloverResultItem>.Failure("في الخريطة صفّان لنفس (المستوى + الشعبة) المصدر — احذف التكرار أولاً.", ErrorType.Validation);

            // تحقق الخريطة مقدماً دفعة واحدة — صف فاسد لا يُسقط عشرات الطلاب بأخطاء متفرقة
            foreach (var mapping in request.Mappings)
            {
                var level = await _levels.GetByIdAsync(mapping.TargetLevelId, cancellationToken);
                if (level is null)
                    return OperationResult<BulkRolloverResultItem>.Failure($"مستوى هدف غير موجود في الخريطة (معرّف {mapping.TargetLevelId}).", ErrorType.Validation);
                if (!level.IsActive)
                    return OperationResult<BulkRolloverResultItem>.Failure($"مستوى الهدف «{level.Name}» معطّل — فعّله من البنية الأكاديمية أولاً.", ErrorType.BusinessRule);

                if (mapping.TargetStreamId is not null)
                {
                    // نفس حارس التسجيل الفردي: الشعبة تتبع مستوى الهدف وتكون فعّالة
                    var targetStreams = await _streams.GetByLevelIdAsync(mapping.TargetLevelId, cancellationToken);
                    var stream = targetStreams.FirstOrDefault(s => s.Id == mapping.TargetStreamId.Value);
                    if (stream is null)
                        return OperationResult<BulkRolloverResultItem>.Failure($"شعبة هدف لا تتبع مستواها في الخريطة (معرّف {mapping.TargetStreamId}).", ErrorType.Validation);
                    if (!stream.IsActive)
                        return OperationResult<BulkRolloverResultItem>.Failure($"شعبة الهدف «{stream.Name}» معطّلة — فعّلها أو عدّل الخريطة.", ErrorType.BusinessRule);
                }
            }

            // تر-4: الحقوق = افتراضي السنة الهدف للجميع (D-66) — الإعفاءات الفردية لا تُنسخ (تُعاد يدوياً)
            var feeCentimes = targetYear.RegistrationFeeCentimes;

            var rows = new List<RolloverStudentResult>();
            foreach (var studentId in request.StudentIds.Distinct())
            {
                try
                {
                    // تسجيل المصدر النشط يحدد (المستوى/الشعبة) الحقيقيين الآن — لا وثوق بمدخلات الواجهة
                    var source = await _enrollments.GetActiveForStudentInYearAsync(studentId, request.SourceYearId, cancellationToken);
                    if (source is null)
                    {
                        rows.Add(new RolloverStudentResult(studentId, RolloverOutcome.Skipped, "لا تسجيل نشط له في سنة المصدر — تغيّر بعد المعاينة."));
                        continue;
                    }

                    var mapping = request.Mappings.FirstOrDefault(m =>
                        m.SourceLevelId == source.LevelId && m.SourceStreamId == source.StreamId);
                    if (mapping is null)
                    {
                        // سطر اصطياد (D-121): استبعاد من كتلة تنظيمية-مالية يبقى مرئياً في التقرير والسجل معاً
                        _logger.LogWarning("Rollover failed: no mapping row for student {StudentId} (level {LevelId}, stream {StreamId}) into year {TargetYearId}",
                            studentId, source.LevelId, source.StreamId, request.TargetYearId);
                        rows.Add(new RolloverStudentResult(studentId, RolloverOutcome.Failed, "لا صف في الخريطة لمستواه/شعبته الحاليين."));
                        continue;
                    }

                    var result = await _register.ExecuteAsync(new RegisterAnnualEnrollmentRequest(
                        studentId, request.TargetYearId, mapping.TargetLevelId, mapping.TargetStreamId,
                        feeCentimes, null), cancellationToken);

                    if (result.IsSuccess)
                    {
                        rows.Add(new RolloverStudentResult(studentId, RolloverOutcome.Success, null));
                    }
                    else
                    {
                        // التكرار الودّي = تخطٍّ (إعادة الضغط آمنة روح D-87) — وغيره فشل بسببه الظاهر
                        var outcome = result.ErrorType == ErrorType.Conflict ? RolloverOutcome.Skipped : RolloverOutcome.Failed;
                        _logger.LogWarning("Rollover {Outcome} for student {StudentId} into year {TargetYearId}: {Reason}",
                            outcome, studentId, request.TargetYearId, result.ErrorMessage);
                        rows.Add(new RolloverStudentResult(studentId, outcome, result.ErrorMessage));
                    }
                }
                catch (OperationCanceledException) { throw; }   // D-64
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected rollover failure for student {StudentId} into year {TargetYearId}", studentId, request.TargetYearId);
                    rows.Add(new RolloverStudentResult(studentId, RolloverOutcome.Failed, "حدث خطأ غير متوقع لهذا الطالب."));
                }
            }

            return OperationResult<BulkRolloverResultItem>.Success(new BulkRolloverResultItem(rows));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute bulk rollover from year {SourceYearId} to year {TargetYearId}",
                request.SourceYearId, request.TargetYearId);
            return OperationResult<BulkRolloverResultItem>.Failure("حدث خطأ غير متوقع أثناء الترحيل الجماعي.", ErrorType.Unexpected);
        }
    }
}