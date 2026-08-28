using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Billing;
using EduMaster.Application.Common;
using EduMaster.Application.Printing;
using EduMaster.Application.Reports;
using EduMaster.Domain.Billing;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests.Printing
{
    /// <summary>تجميع نموذج الإيصال للطباعة (6.3 — ط-ج): تركيب الأوصاف (D-128) + حساب غير المخصص + مرآة الصرف + ترويسة الهوية وسقوطها (D-131)</summary>
    public sealed class GetReceiptPrintModelHandlerTests
    {
        private static readonly DateTime Now = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        private static ReceiptPrintRead NewReceipt(PaymentKind kind) => new(
            Id: 1, ReceiptNo: 101, Kind: kind,
            StudentId: 2, StudentName: "أمين بن يوسف",
            PayerName: "الولي محمد",
            AmountCentimes: 80000, PaidOn: new DateTime(2026, 8, 26), Note: null,
            Allocations: kind == PaymentKind.Receipt
                ? new List<ReceiptAllocationLineRaw> { new(7, 30000), new(9, 20000) }
                : new List<ReceiptAllocationLineRaw>());

        private static IEnumerable<StudentChargeItem> NewCharges() => new List<StudentChargeItem>
        {
            // StudentChargeItem(Id, StudentId, Kind, SourceDescription, OriginalAmountCentimes, AmountCentimes, Status, AdjustmentNote, CreatedAtUtc, AllocatedCentimes)
            new(7, 2, ChargeKind.RegistrationFee, "حقوق تسجيل 2026-2027", 30000, 30000, ChargeStatus.Active, null, new DateTime(2026, 7, 1), 30000),
            new(9, 2, ChargeKind.SessionBundle, "حصص رياضيات ×4", 20000, 20000, ChargeStatus.Active, null, new DateTime(2026, 8, 1), 20000),
        };

        private static GetReceiptPrintModelHandler NewHandler(ReportRepoFake reports, ChargeRepoFake charges, SchoolInfoRepoFake school)
            => new(reports, charges, school, NullLogger<GetReceiptPrintModelHandler>.Instance);

        [Fact]
        public async Task Success_Receipt_ComposesDescriptionsAndComputesUnallocated()
        {
            var reports = new ReportRepoFake { ReceiptToReturn = NewReceipt(PaymentKind.Receipt) };
            var charges = new ChargeRepoFake { ForStudentToReturn = NewCharges() };
            var school = new SchoolInfoRepoFake();   // «مدرسة النجاح» مُهيأة افتراضياً

            var result = await NewHandler(reports, charges, school).ExecuteAsync(1);

            Assert.True(result.IsSuccess);
            var model = result.Value!;
            Assert.Equal("إيصال قبض", model.DocumentTitle);
            Assert.Equal("#000101", model.ReceiptNoText);
            Assert.Equal("أمين بن يوسف", model.StudentName);
            Assert.Equal("الولي محمد", model.PayerName);
            Assert.Equal(80000, model.AmountCentimes);

            // الوصف مركّب من قائمة مستحقات الطالب (D-128) — لا من SQL
            Assert.Equal(2, model.Allocations.Count);
            Assert.Equal("حقوق تسجيل 2026-2027", model.Allocations[0].SourceDescription);
            Assert.Equal(30000, model.Allocations[0].AmountCentimes);
            Assert.Equal("حصص رياضيات ×4", model.Allocations[1].SourceDescription);
            Assert.Equal(20000, model.Allocations[1].AmountCentimes);

            // غير المخصص يُحسب في النموذج النقي: 80000 − (30000+20000) = 30000
            Assert.Equal(30000, model.UnallocatedCentimes);
            Assert.True(model.HasUnallocated);

            // الترويسة من هوية المدرسة
            Assert.Equal("مدرسة النجاح", model.Header.SchoolName);
            Assert.Equal("0550001122", model.Header.SchoolPhone);
        }

        [Fact]
        public async Task Success_Refund_MirrorsTitleAndZeroUnallocated()
        {
            var reports = new ReportRepoFake { ReceiptToReturn = NewReceipt(PaymentKind.Refund) };
            var charges = new ChargeRepoFake { ForStudentToReturn = NewCharges() };
            var school = new SchoolInfoRepoFake();

            var result = await NewHandler(reports, charges, school).ExecuteAsync(1);

            Assert.True(result.IsSuccess);
            var model = result.Value!;
            Assert.Equal("إيصال صرف (استرجاع)", model.DocumentTitle);   // المرآة من Kind — لا نموذج ثانٍ (ط-3)
            Assert.True(model.IsRefund);
            Assert.False(model.HasAllocations);
            Assert.Equal(0, model.UnallocatedCentimes);
            Assert.False(model.HasUnallocated);
        }

        [Fact]
        public async Task ReceiptMissing_NotFound_AndReadsNothingFurther()
        {
            var reports = new ReportRepoFake { ReceiptToReturn = null };
            var charges = new ChargeRepoFake();
            var school = new SchoolInfoRepoFake();

            var result = await NewHandler(reports, charges, school).ExecuteAsync(999);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal("الإيصال غير موجود.", result.ErrorMessage);
            Assert.False(charges.Called);   // الوجود أولاً — لا قراءات زائدة لإيصال مفقود
            Assert.False(school.Called);
        }

        [Fact]
        public async Task SchoolInfoMissing_FallsBackToProductName()   // D-131
        {
            var reports = new ReportRepoFake { ReceiptToReturn = NewReceipt(PaymentKind.Receipt) };
            var charges = new ChargeRepoFake { ForStudentToReturn = NewCharges() };
            var school = new SchoolInfoRepoFake { EntityToReturn = null };

            var result = await NewHandler(reports, charges, school).ExecuteAsync(1);

            Assert.True(result.IsSuccess);
            Assert.Equal("EduMaster", result.Value!.Header.SchoolName);   // السقوط على اسم المنتج
            Assert.Null(result.Value.Header.SchoolPhone);
            Assert.Null(result.Value.Header.LogoPath);
        }

        [Fact]
        public async Task Cancellation_Propagates()   // D-64
        {
            var reports = new ReportRepoFake { ToThrow = new OperationCanceledException() };
            var handler = NewHandler(reports, new ChargeRepoFake(), new SchoolInfoRepoFake());

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => handler.ExecuteAsync(1, new CancellationToken(canceled: true)));
        }

        [Fact]
        public async Task UnexpectedException_ArabicFailure()   // D-24
        {
            var reports = new ReportRepoFake { ToThrow = new InvalidOperationException("raw boom") };

            var result = await NewHandler(reports, new ChargeRepoFake(), new SchoolInfoRepoFake()).ExecuteAsync(1);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorType.Unexpected, result.ErrorType);
            Assert.Contains("خطأ غير متوقع", result.ErrorMessage);
            Assert.DoesNotContain("boom", result.ErrorMessage);
        }

        // ---------- زائفون داخليون (بتواقيع العقود الحقيقية المقروءة حرفياً) ----------
        private sealed class ReportRepoFake : IReportRepository
        {
            public ReceiptPrintRead? ReceiptToReturn { get; set; }
            public Exception? ToThrow { get; set; }

            public Task<ReceiptPrintRead?> GetReceiptForPrintAsync(int paymentId, CancellationToken cancellationToken = default)
            {
                if (ToThrow is not null) throw ToThrow;
                return Task.FromResult(ReceiptToReturn);
            }

            public Task<StudentPaymentsRead> GetPaymentsWithAllocationsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            // 6.4-أ: عضوا العقد الجديدان — غير مستعملين في اختبارات نموذج الإيصال
            public Task<IReadOnlyList<AttendanceMarkRaw>> GetAttendanceMarksForPeriodAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public Task<IReadOnlyList<EnrollmentBalanceRaw>> GetActiveEnrollmentBalancesAsync(CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        private sealed class ChargeRepoFake : IChargeRepository
        {
            public IEnumerable<StudentChargeItem> ForStudentToReturn { get; set; } = Array.Empty<StudentChargeItem>();
            public bool Called { get; private set; }

            public Task<IEnumerable<StudentChargeItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default)
            {
                Called = true;
                return Task.FromResult(ForStudentToReturn);
            }

            public Task AddAsync(Charge charge, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<Charge?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task UpdateAsync(Charge charge, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IEnumerable<OpenChargeItem>> GetOpenForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<IEnumerable<DebtorItem>> GetDebtorsAsync(string? searchTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<long> GetAllocatedForChargeAsync(int chargeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<bool> HasAnyForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private sealed class SchoolInfoRepoFake : ISchoolInfoRepository
        {
            public SchoolInfo? EntityToReturn { get; set; } = SchoolInfo.Load(1, "مدرسة النجاح", "0550001122", "حي السلام", null,
                Now, 1, null, null);
            public bool Called { get; private set; }

            public Task<SchoolInfo?> GetAsync(CancellationToken cancellationToken = default)
            {
                Called = true;
                return Task.FromResult(EntityToReturn);
            }

            public Task AddAsync(SchoolInfo info, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task UpdateAsync(SchoolInfo info, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }
    }
}