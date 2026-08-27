using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Settings;

public sealed record SetSchoolLogoRequest(string? SourcePath);   // null = إزالة اللوغو

/// <summary>لوغو المدرسة عبر قناة الصور القائمة (D-38): النسخ قبل المعاملة، والقاعدة تحفظ اسم الملف فقط · الغياب التام للصف ← إنشاء ذاتي بالاسم الافتراضي «EduMaster» (D-131)</summary>
public sealed class SetSchoolLogoHandler
{
    private readonly ISchoolInfoRepository _schoolInfo;
    private readonly IImageStore _imageStore;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetSchoolLogoHandler> _logger;

    public SetSchoolLogoHandler(ISchoolInfoRepository schoolInfo, IImageStore imageStore, IClock clock,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, ILogger<SetSchoolLogoHandler> logger)
    {
        _schoolInfo = schoolInfo;
        _imageStore = imageStore;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult> ExecuteAsync(SetSchoolLogoRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var info = await _schoolInfo.GetAsync(cancellationToken);
            var isNew = info is null;

            // D-131: الإنشاء الذاتي عند الغياب يكون باسم المنتج — يُعدَّل لاحقاً من قسم المدرسة
            info ??= SchoolInfo.Create("EduMaster", null, null, _clock.UtcNow, _currentUser.UserAccountId);

            // النسخ قبل المعاملة (D-38) — null يعني إزالة اللوغو
            string? storedLogo = null;
            if (!string.IsNullOrWhiteSpace(request.SourcePath))
            {
                try
                {
                    storedLogo = await _imageStore.SaveFromPathAsync(request.SourcePath, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    return OperationResult.Failure("الصورة غير مدعومة أو يتجاوز حجمها 5MB — المسموح: jpg / png.", ErrorType.Validation);
                }
            }

            info.ChangeLogo(storedLogo, _clock.UtcNow, _currentUser.UserAccountId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            if (isNew)
                await _schoolInfo.AddAsync(info, cancellationToken);
            else
                await _schoolInfo.UpdateAsync(info, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success();
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            // سطر الاصطياد (D-121)
            _logger.LogWarning(dex, "School logo change rejected by domain rule");
            return OperationResult.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to set school logo");
            return OperationResult.Failure("حدث خطأ غير متوقع أثناء حفظ اللوغو.", ErrorType.Unexpected);
        }
    }
}