using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Enrollments;

/// <summary>
/// تعديل التسجيل السنوي (D-72): الحقوق دائماً · المستوى/الشعبة بحارس «لا أفواج نشطة» (D-54 — يُفعَّل في 2.4)
/// · السنة ثابتة (الخطأ فيها = انسحاب + تسجيل جديد)
/// </summary>
public sealed record UpdateAnnualEnrollmentRequest(
    int EnrollmentId,
    int LevelId,
    int? StreamId,
    long AgreedRegistrationFeeCentimes,
    string? RegistrationFeeNote);

public sealed class UpdateAnnualEnrollmentHandler
{
    private readonly IAnnualEnrollmentRepository _enrollments;
    private readonly ILevelRepository _levels;
    private readonly IStreamRepository _streams;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAnnualEnrollmentHandler> _logger;

    public UpdateAnnualEnrollmentHandler(IAnnualEnrollmentRepository enrollments, ILevelRepository levels,
        IStreamRepository streams, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateAnnualEnrollmentHandler> logger)
    {
        _enrollments = enrollments;
        _levels = levels;
        _streams = streams;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(UpdateAnnualEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AgreedRegistrationFeeCentimes < 0)
            return OperationResult.Failure("حقوق التسجيل لا يمكن أن تكون سالبة.", ErrorType.Validation);

        try
        {
            var enrollment = await _enrollments.GetByIdAsync(request.EnrollmentId, cancellationToken);
            if (enrollment is null)
                return OperationResult.Failure("التسجيل غير موجود.", ErrorType.NotFound);

            if (!enrollment.IsActive)
                return OperationResult.Failure("لا يمكن تعديل تسجيل منسحب — سجّله من جديد بصف جديد.", ErrorType.BusinessRule);

            var levelOrStreamChanged = request.LevelId != enrollment.LevelId || request.StreamId != enrollment.StreamId;
            if (levelOrStreamChanged)
            {
                // حارس D-54 — stub اليوم، يُفعَّل فعلياً في 2.4 حين يوجد ClassGroupEnrollments
                if (await _enrollments.HasActiveGroupEnrollmentsAsync(enrollment.Id, cancellationToken))
                    return OperationResult.Failure("لا يمكن تغيير المستوى/الشعبة — للطالب أفواج نشطة في هذه السنة. اسحبه منها أولاً.", ErrorType.BusinessRule);

                var level = await _levels.GetByIdAsync(request.LevelId, cancellationToken);
                if (level is null)
                    return OperationResult.Failure("المستوى المحدد غير موجود.", ErrorType.Validation);
                if (!level.IsActive)
                    return OperationResult.Failure("المستوى المحدد معطّل — فعّله أولاً.", ErrorType.BusinessRule);

                if (request.StreamId is not null)
                {
                    var levelStreams = await _streams.GetByLevelIdAsync(request.LevelId, cancellationToken);
                    var stream = levelStreams.FirstOrDefault(s => s.Id == request.StreamId.Value);
                    if (stream is null)
                        return OperationResult.Failure("الشعبة المحددة لا تتبع المستوى المختار.", ErrorType.Validation);
                    if (!stream.IsActive)
                        return OperationResult.Failure("الشعبة المحددة معطّلة — فعّلها أو اختر غيرها.", ErrorType.BusinessRule);
                }

                enrollment.UpdateLevelStream(request.LevelId, request.StreamId, _clock.UtcNow, _currentUser.UserAccountId);
            }

            enrollment.UpdateRegistrationFee(request.AgreedRegistrationFeeCentimes, request.RegistrationFeeNote,
                _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _enrollments.UpdateAsync(enrollment, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update annual enrollment {EnrollmentId}", request.EnrollmentId);
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء تعديل التسجيل.", ErrorType.Unexpected);
        }
    }
}