using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>شراء الحصص (D-91/D-99) + توليد مستحق الحزمة في المعاملة ذاتها (D-103/D-96 — يُتخطّى عند سعر 0)</summary>
public sealed class PurchaseSessionsHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

    private static Domain.Enrollments.ClassGroupEnrollment BuildEnrollment(EnrollmentStatus status, long agreedUnitPriceCentimes = 35000) =>
        Domain.Enrollments.ClassGroupEnrollment.Load(
            id: 5, classGroupId: 1, studentId: 2, annualEnrollmentId: 3,
            status: status,
            snapshotUnitPriceCentimes: agreedUnitPriceCentimes, agreedUnitPriceCentimes: agreedUnitPriceCentimes, discountNote: null,
            enrolledAtUtc: Now, withdrawnAtUtc: status == EnrollmentStatus.Withdrawn ? Now : null,
            createdAtUtc: Now, createdByUserId: 1, updatedAtUtc: null, updatedByUserId: null);

    private static (PurchaseSessionsHandler handler, FakeGroupSessionPurchaseRepository purchases, FakeChargeRepository charges, FakeUnitOfWork uow) Build(
        Domain.Enrollments.ClassGroupEnrollment? enrollment)
    {
        var purchases = new FakeGroupSessionPurchaseRepository();
        var enrollments = new FakeClassGroupEnrollmentRepository { EntityToReturn = enrollment };
        var charges = new FakeChargeRepository();
        var uow = new FakeUnitOfWork();
        var handler = new PurchaseSessionsHandler(
            purchases, enrollments, charges, new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<PurchaseSessionsHandler>.Instance);
        return (handler, purchases, charges, uow);
    }

    [Fact]
    public async Task ActiveEnrollment_ValidCount_PurchasesAndCommits()
    {
        var (handler, purchases, _, uow) = Build(BuildEnrollment(EnrollmentStatus.Active));

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(5, 4, "حزمة شهر"));

        Assert.True(result.IsSuccess);
        Assert.Single(purchases.Captured);
        Assert.Equal(5, purchases.Captured[0].ClassGroupEnrollmentId);
        Assert.Equal(4, purchases.Captured[0].SessionsCount);
        Assert.Equal("حزمة شهر", purchases.Captured[0].Note);
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task Purchase_GeneratesBundleCharge_Atomically()   // D-103/D-96: 4 × 35000 = 140000 سنتيم
    {
        var (handler, purchases, charges, uow) = Build(BuildEnrollment(EnrollmentStatus.Active));

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(5, 4, null));

        Assert.True(result.IsSuccess);
        var charge = Assert.Single(charges.Added);
        Assert.Equal(ChargeKind.SessionBundle, charge.Kind);
        Assert.Equal(2, charge.StudentId);                                // طالب التسجيل لا مُمرَّر
        Assert.Equal(purchases.Captured[0].Id, charge.GroupSessionPurchaseId);   // معرف المزيّف (SetId عبر InternalsVisibleTo)
        Assert.Equal(140000, charge.OriginalAmountCentimes);
        Assert.Equal(140000, charge.AmountCentimes);
        Assert.Equal(1, uow.CommittedCount);                              // معاً في معاملة واحدة
    }

    [Fact]
    public async Task Purchase_FreeBundle_SkipsCharge()   // سعر متفق 0 (مجاني صريح D-65) ← لا مستحق (D-103)
    {
        var (handler, purchases, charges, uow) = Build(BuildEnrollment(EnrollmentStatus.Active, agreedUnitPriceCentimes: 0));

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(5, 4, null));

        Assert.True(result.IsSuccess);
        Assert.Single(purchases.Captured);     // الشراء يتم (رصيد الحصص يزيد)
        Assert.Empty(charges.Added);           // لكن بلا مستحق مالي
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task NonPositiveCount_ValidationError_WithoutTouchingAnything()
    {
        var (handler, purchases, charges, uow) = Build(BuildEnrollment(EnrollmentStatus.Active));

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(5, 0, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(purchases.Captured);
        Assert.Empty(charges.Added);
        Assert.Equal(0, uow.BeganCount);   // التحقق قبل فتح المعاملة
    }

    [Fact]
    public async Task MissingEnrollment_NotFound()
    {
        var (handler, _, _, _) = Build(null);

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(99, 4, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task WithdrawnEnrollment_BusinessRule_WithoutWriting()
    {
        var (handler, purchases, charges, uow) = Build(BuildEnrollment(EnrollmentStatus.Withdrawn));

        var result = await handler.ExecuteAsync(new PurchaseSessionsRequest(5, 4, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);   // D-99
        Assert.Contains("أعد إلحاقه", result.ErrorMessage);
        Assert.Empty(purchases.Captured);
        Assert.Empty(charges.Added);
        Assert.Equal(0, uow.CommittedCount);
    }
}