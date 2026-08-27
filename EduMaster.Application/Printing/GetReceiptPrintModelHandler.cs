using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using EduMaster.Application.Settings;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Printing;

/// <summary>
/// تجميع نموذج إيصال مفرد للطباعة (ط-2): الدفعة الخام (بمعرفات المستحقات) ← الوصف من قائمة مستحقات الطالب (D-128) ← ترويسة الهوية (ط-7) ·
/// قراءة خالصة بلا معاملة وترمي الإلغاء (D-64)
/// </summary>
public sealed class GetReceiptPrintModelHandler
{
    private readonly IReportRepository _reports;
    private readonly IChargeRepository _charges;
    private readonly ISchoolInfoRepository _schoolInfo;
    private readonly ILogger<GetReceiptPrintModelHandler> _logger;

    public GetReceiptPrintModelHandler(IReportRepository reports, IChargeRepository charges,
        ISchoolInfoRepository schoolInfo, ILogger<GetReceiptPrintModelHandler> logger)
    {
        _reports = reports;
        _charges = charges;
        _schoolInfo = schoolInfo;
        _logger = logger;
    }

    public async Task<OperationResult<ReceiptPrintModel>> ExecuteAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var read = await _reports.GetReceiptForPrintAsync(paymentId, cancellationToken);
            if (read is null)
                return OperationResult<ReceiptPrintModel>.Failure("الإيصال غير موجود.", ErrorType.NotFound);

            // الوصف من قائمة مستحقات الطالب نفسها — D-128 (لا تعبير SQL مكرر)
            var charges = await _charges.GetForStudentAsync(read.StudentId, cancellationToken);
            var descriptions = charges.ToDictionary(c => c.Id, c => c.SourceDescription);

            var allocations = read.Allocations
                .Select(a => new ReceiptAllocationPrintLine(
                    descriptions.TryGetValue(a.ChargeId, out var desc) ? desc : "مستحق غير ظاهر في الكشف",
                    a.AmountCentimes))
                .ToList();

            // ترويسة الهوية — السقوط على اسم المنتج عبر DisplayName نفسه (D-131 — مصدر واحد)
            var school = await _schoolInfo.GetAsync(cancellationToken);
            var item = school is null
                ? new SchoolInfoItem(0, string.Empty, null, null, null)
                : new SchoolInfoItem(school.Id, school.Name, school.Phone, school.Address, school.LogoPath);
            var header = new PrintHeader(item.DisplayName, item.Phone, item.Address, item.LogoPath);

            return OperationResult<ReceiptPrintModel>.Success(new ReceiptPrintModel(
                header, read.Kind, read.ReceiptNo, read.PaidOn,
                read.StudentName, read.PayerName, read.AmountCentimes, read.Note,
                allocations));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build receipt print model for payment {PaymentId}", paymentId);
            return OperationResult<ReceiptPrintModel>.Failure("حدث خطأ غير متوقع أثناء تجهيز الإيصال للطباعة.", ErrorType.Unexpected);
        }
    }
}