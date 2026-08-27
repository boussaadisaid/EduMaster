using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Settings;

/// <summary>قراءة هوية المدرسة — خالصة بلا معاملة وترمي الإلغاء (D-64) · الغياب ← نسخة افتراضية فارغة يسقط اسمها على «EduMaster» (D-131)</summary>
public sealed class GetSchoolInfoHandler
{
    private readonly ISchoolInfoRepository _schoolInfo;
    private readonly ILogger<GetSchoolInfoHandler> _logger;

    public GetSchoolInfoHandler(ISchoolInfoRepository schoolInfo, ILogger<GetSchoolInfoHandler> logger)
    {
        _schoolInfo = schoolInfo;
        _logger = logger;
    }

    public async Task<OperationResult<SchoolInfoItem>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _schoolInfo.GetAsync(cancellationToken);
            var item = info is null
                ? new SchoolInfoItem(0, string.Empty, null, null, null)
                : new SchoolInfoItem(info.Id, info.Name, info.Phone, info.Address, info.LogoPath);
            return OperationResult<SchoolInfoItem>.Success(item);
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load school info");
            return OperationResult<SchoolInfoItem>.Failure("حدث خطأ غير متوقع أثناء تحميل معلومات المدرسة.", ErrorType.Unexpected);
        }
    }
}