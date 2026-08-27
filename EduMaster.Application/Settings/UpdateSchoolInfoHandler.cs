using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Settings;

public sealed record UpdateSchoolInfoRequest(string Name, string? Phone, string? Address);

/// <summary>تحرير هوية المدرسة (الاسم/الهاتف/العنوان) — إنشاء أولي عند الغياب أو تحديث · معاملة حول الكتابة · اللوغو من handler شقيق (قناة D-38)</summary>
public sealed class UpdateSchoolInfoHandler
{
    private readonly ISchoolInfoRepository _schoolInfo;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSchoolInfoHandler> _logger;

    public UpdateSchoolInfoHandler(ISchoolInfoRepository schoolInfo, IClock clock, ICurrentUserService currentUser,
        IUnitOfWork unitOfWork, ILogger<UpdateSchoolInfoHandler> logger)
    {
        _schoolInfo = schoolInfo;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(UpdateSchoolInfoRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var info = await _schoolInfo.GetAsync(cancellationToken);
            var isNew = info is null;

            // طابع واحد لمسارَي الإنشاء والتحديث — ذرّية اللحظة
            var utcNow = _clock.UtcNow;
            var userId = _currentUser.UserAccountId;

            info = isNew
                ? SchoolInfo.Create(request.Name, request.Phone, request.Address, utcNow, userId)
                : info!;

            if (!isNew)
                info.Update(request.Name, request.Phone, request.Address, utcNow, userId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            if (isNew)
                await _schoolInfo.AddAsync(info, cancellationToken);
            else
                await _schoolInfo.UpdateAsync(info, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(info.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            // سطر الاصطياد (D-121): يبقى دائماً — لا تتبع لرفض القواعد بلا أصله
            _logger.LogWarning(dex, "School info update rejected by domain rule (name length {NameLength})", request.Name?.Length ?? 0);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update school info (name length {NameLength})", request.Name?.Length ?? 0);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء حفظ معلومات المدرسة.", ErrorType.Unexpected);
        }
    }
}