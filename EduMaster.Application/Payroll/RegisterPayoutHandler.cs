using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using EduMaster.Domain.Payroll;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.Payroll;

/// <summary>
/// تسجيل إيصال صرف (D-116/D-125 — مرآة RegisterPaymentHandler للقبض): موجب = صرف/سلفة · سالب = قيد تصحيح لإيصال سابق (بملاحظة إلزامية — الكيان يحرس).
/// الصرف على الرصيد الجاري: يُحسب «المعتمد − المصروف» للمستفيد عبر التاريخ — تجاوزه = سلفة تتطلب ملاحظة (D-116: «سلفة حرة بملاحظة»).
/// رقم الإيصال MAX+1 داخل المعاملة والفريد يحرسه قاعدةً (مرآة D-105) — تسلسل موحّد للفريقين بلا فجوات.
/// ملاحظة: التصحيح العكسي ليس handler رابعاً — هو هذا الـhandler نفسه بمبلغ سالب.
/// </summary>
public sealed record RegisterPayoutRequest(PayeeKind PayeeKind, int? TeacherId, int? EmployeeId, int? PayrollRunId, long AmountCentimes, string? Note);

public sealed class RegisterPayoutHandler
{
    private readonly IPayoutRepository _payouts;
    private readonly IPayrollLineRepository _lines;
    private readonly ITeacherRepository _teachers;
    private readonly IEmployeeRepository _employees;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RegisterPayoutHandler> _logger;

    public RegisterPayoutHandler(
        IPayoutRepository payouts,
        IPayrollLineRepository lines,
        ITeacherRepository teachers,
        IEmployeeRepository employees,
        IClock clock,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<RegisterPayoutHandler> logger)
    {
        _payouts = payouts;
        _lines = lines;
        _teachers = teachers;
        _employees = employees;
        _clock = clock;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OperationResult<int>> ExecuteAsync(RegisterPayoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // وجود المستفيد (اتساق النوع مع المعرّف يحرسه الكيان — هنا الوجود فقط)
            int payeeId;
            if (request.PayeeKind == PayeeKind.Teacher)
            {
                if (request.TeacherId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الأستاذ.", ErrorType.Validation);
                payeeId = request.TeacherId.Value;
                if (await _teachers.GetByIdAsync(payeeId, cancellationToken) is null)
                    return OperationResult<int>.Failure("الأستاذ غير موجود.", ErrorType.NotFound);
            }
            else if (request.PayeeKind == PayeeKind.Employee)
            {
                if (request.EmployeeId is null or <= 0)
                    return OperationResult<int>.Failure("حدد الموظف.", ErrorType.Validation);
                payeeId = request.EmployeeId.Value;
                if (await _employees.GetByIdAsync(payeeId, cancellationToken) is null)
                    return OperationResult<int>.Failure("الموظف غير موجود.", ErrorType.NotFound);
            }
            else
            {
                return OperationResult<int>.Failure("نوع المستفيد غير صالح.", ErrorType.Validation);
            }

            // حارس السلفة (صرف موجب يتجاوز الرصيد الجاري ⇒ ملاحظة إلزامية) — قيد التصحيح السالب يتخطاها بطبيعته
            if (request.AmountCentimes > 0)
            {
                var approved = (await _lines.GetApprovedTotalsByPayeeAsync(cancellationToken))
                    .FirstOrDefault(t => t.PayeeKind == request.PayeeKind && t.PayeeId == payeeId)?.TotalCentimes ?? 0;
                var paid = (await _payouts.GetTotalsByPayeeAsync(cancellationToken))
                    .FirstOrDefault(t => t.PayeeKind == request.PayeeKind && t.PayeeId == payeeId)?.TotalCentimes ?? 0;
                var balance = approved - paid;

                if (request.AmountCentimes > balance && string.IsNullOrWhiteSpace(request.Note))
                    return OperationResult<int>.Failure(
                        $"الصرف يتجاوز رصيده الحالي ({balance / 100m:0.00} دج) — هذه سلفة: اذكر الملاحظة لتوثيقها.", ErrorType.Validation);
            }

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var receiptNo = await _payouts.GetNextReceiptNoAsync(cancellationToken);   // داخل المعاملة — الفريد يحرس (D-105)
            var payout = Payout.Create(request.PayeeKind, request.TeacherId, request.EmployeeId, request.PayrollRunId,
                request.AmountCentimes, request.Note, receiptNo, _clock.UtcNow, _currentUser.UserAccountId);

            await _payouts.AddAsync(payout, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            return OperationResult<int>.Success(payout.Id);
        }
        catch (DomainException dex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogWarning(dex, "Domain rejection while registering payout {PayeeKind} (teacher {TeacherId} / employee {EmployeeId}) — D-121 trap", request.PayeeKind, request.TeacherId, request.EmployeeId);
            return OperationResult<int>.Failure(dex.Message, ErrorType.Validation);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to register payout for {PayeeKind} (teacher {TeacherId} / employee {EmployeeId})", request.PayeeKind, request.TeacherId, request.EmployeeId);
            return OperationResult<int>.Failure("حدث خطأ غير متوقع أثناء تسجيل الصرف.", ErrorType.Unexpected);
        }
    }
}