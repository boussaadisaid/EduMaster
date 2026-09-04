using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Application.Common;
using EduMaster.Application.Enrollments;
using EduMaster.Application.Pricing;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Domain.ClassGroups;
using EduMaster.Domain.Enrollments;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

public sealed class TransferGroupEnrollmentHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EqualPrice_TransfersPositiveBalance_AndCreatesNewEnrollment()
    {
        var source = Enrollment(10, 100, 7, 3000);
        var target = Group(200, 1, 1, 8, "فوج ب");
        var currentGroup = Group(100, 1, 1, 7, "فوج أ");
        var annual = Annual(30);
        var groupRepo = new FakeGroups(currentGroup, target);
        var enrollRepo = new FakeEnrollments(source, targetStudentId: 7, targetGroupId: 200);
        var balanceRepo = new FakeSessionBalanceRepository(new SessionBalanceSnapshot(10, 0, 0, 3));
        var transferRepo = new FakeSessionTransferRepository();
        var uow = new FakeUow();

        var handler = Build(enrollRepo, groupRepo, annual, 3000, balanceRepo, transferRepo, uow);

        var result = await handler.ExecuteAsync(new TransferGroupEnrollmentRequest(source.Id, target.Id, null));

        Assert.True(result.IsSuccess);
        Assert.Equal(10, balanceRepo.LastRequestedId);
        Assert.Single(transferRepo.Added);
        Assert.Equal(7, transferRepo.Added[0].SessionsCount);
        Assert.Equal(source.Id, transferRepo.Added[0].FromClassGroupEnrollmentId);
        Assert.Equal(result.Value, transferRepo.Added[0].ToClassGroupEnrollmentId);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RollbackCount);
        Assert.False(source.IsActive);
        Assert.NotNull(enrollRepo.Added);
        Assert.Equal(200, enrollRepo.Added!.ClassGroupId);
        Assert.Equal(3000, enrollRepo.Added.AgreedUnitPriceCentimes);
    }

    [Fact]
    public async Task DifferentPrice_RejectsBeforeTransaction_AndDoesNotChangeEnrollment()
    {
        var source = Enrollment(10, 100, 7, 3000);
        var target = Group(200, 1, 2, 7, "فوج ب");
        var currentGroup = Group(100, 1, 1, 7, "فوج أ");
        var annual = Annual(30);
        var groupRepo = new FakeGroups(currentGroup, target);
        var enrollRepo = new FakeEnrollments(source, targetStudentId: 7, targetGroupId: 200);
        var balanceRepo = new FakeSessionBalanceRepository(new SessionBalanceSnapshot(10, 0, 0, 3));
        var transferRepo = new FakeSessionTransferRepository();
        var uow = new FakeUow();

        var handler = Build(enrollRepo, groupRepo, annual, 5000, balanceRepo, transferRepo, uow);

        var result = await handler.ExecuteAsync(new TransferGroupEnrollmentRequest(source.Id, target.Id, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BusinessRule, result.ErrorType);
        Assert.Equal(0, uow.BeganCount);
        Assert.Empty(transferRepo.Added);
        Assert.True(source.IsActive);
        Assert.Null(enrollRepo.Added);
    }

    [Fact]
    public async Task EqualPrice_WithZeroOrNegativeBalance_DoesNotCreateTransfer()
    {
        var source = Enrollment(10, 100, 7, 3000);
        var target = Group(200, 1, 1, 7, "فوج ب");
        var currentGroup = Group(100, 1, 1, 7, "فوج أ");
        var annual = Annual(30);
        var groupRepo = new FakeGroups(currentGroup, target);
        var enrollRepo = new FakeEnrollments(source, targetStudentId: 7, targetGroupId: 200);
        var balanceRepo = new FakeSessionBalanceRepository(new SessionBalanceSnapshot(2, 0, 0, 5));
        var transferRepo = new FakeSessionTransferRepository();
        var uow = new FakeUow();

        var handler = Build(enrollRepo, groupRepo, annual, 3000, balanceRepo, transferRepo, uow);

        var result = await handler.ExecuteAsync(new TransferGroupEnrollmentRequest(source.Id, target.Id, null));

        Assert.True(result.IsSuccess);
        Assert.Empty(transferRepo.Added);
        Assert.Equal(1, uow.CommittedCount);
        Assert.False(source.IsActive);
    }

    private static TransferGroupEnrollmentHandler Build(
        FakeEnrollments enrollments,
        FakeGroups groups,
        AnnualEnrollment annual,
        long targetPrice,
        FakeSessionBalanceRepository balance,
        FakeSessionTransferRepository transfers,
        FakeUow uow)
    {
        var years = new FakeAcademicYears();
        years.Current = AcademicYear.Load(1, new YearName("2026-2027"), new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30), true, true, 0, Now, 1, null, null);
        var prices = new FakePrices(targetPrice);
        var annuals = new FakeAnnualEnrollments(annual);
        return new TransferGroupEnrollmentHandler(
            enrollments, groups, annuals, years, prices, balance, transfers,
            new FakeClock(), new FakeCurrentUser(), uow,
            NullLogger<TransferGroupEnrollmentHandler>.Instance);
    }

    private static ClassGroupEnrollment Enrollment(int id, int groupId, int studentId, long price)
        => ClassGroupEnrollment.Load(id, groupId, studentId, 30, EnrollmentStatus.Active, price, price, null, Now, null, Now, 1, null, null);

    private static ClassGroup Group(int id, int yearId, int levelId, int subjectId, string name)
        => ClassGroup.Load(id, yearId, levelId, subjectId, null, null, name, null, true, Now, 1, null, null);

    private static AnnualEnrollment Annual(int id)
        => AnnualEnrollment.Load(id, 7, 1, 1, null, EnrollmentStatus.Active, 0, null, Now, null, Now, 1, null, null);

    private sealed class FakeClock : IClock { public DateTime UtcNow => Now; public DateOnly Today => DateOnly.FromDateTime(Now); }
    private sealed class FakeCurrentUser : ICurrentUserService { public int? UserAccountId => 1; public string? Username => "admin"; }
    private sealed class FakeUow : IUnitOfWork
    {
        public int BeganCount { get; private set; }
        public int CommittedCount { get; private set; }
        public int RollbackCount { get; private set; }
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) { BeganCount++; return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken cancellationToken = default) { CommittedCount++; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken = default) { RollbackCount++; return Task.CompletedTask; }
    }

    private sealed class FakeSessionBalanceRepository(SessionBalanceSnapshot snapshot) : ISessionBalanceRepository
    {
        public int LastRequestedId { get; private set; }
        public Task<SessionBalanceSnapshot?> GetAsync(int classGroupEnrollmentId, CancellationToken cancellationToken = default)
        { LastRequestedId = classGroupEnrollmentId; return Task.FromResult<SessionBalanceSnapshot?>(snapshot); }
    }

    private sealed class FakeSessionTransferRepository : IGroupSessionTransferRepository
    {
        public List<GroupSessionTransfer> Added { get; } = new();
        public Task AddAsync(GroupSessionTransfer transfer, CancellationToken cancellationToken = default) { transfer.SetId(1); Added.Add(transfer); return Task.CompletedTask; }
    }

    private sealed class FakeEnrollments(ClassGroupEnrollment source, int targetStudentId, int targetGroupId) : IClassGroupEnrollmentRepository
    {
        public ClassGroupEnrollment Source { get; } = source;
        public ClassGroupEnrollment? Added { get; private set; }
        public Task<ClassGroupEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ClassGroupEnrollment?>(Source);
        public Task AddAsync(ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default) { enrollment.SetId(900); Added = enrollment; return Task.CompletedTask; }
        public Task UpdateAsync(ClassGroupEnrollment enrollment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> AnyActiveForStudentInGroupAsync(int classGroupId, int studentId, CancellationToken cancellationToken = default) => Task.FromResult(classGroupId == targetGroupId && studentId == targetStudentId && false);
        public Task<int> CountActiveInGroupAsync(int classGroupId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IEnumerable<ClassGroupEnrollmentListItem>> GetForGroupAsync(int classGroupId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<StudentGroupEnrollmentItem>> GetForStudentAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ClassGroupEnrollment>> GetActiveByAnnualEnrollmentIdAsync(int annualEnrollmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<ClassGroupListItem>> GetTransferTargetsAsync(int groupEnrollmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<ClassGroupListItem>> GetEnrollableGroupsForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeGroups(ClassGroup source, ClassGroup target) : IClassGroupRepository
    {
        public Task<ClassGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ClassGroup?>(id == source.Id ? source : id == target.Id ? target : null);
        public Task<IReadOnlyList<int>> GetStreamIdsAsync(int classGroupId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        public Task<bool> AnyWithNameInYearAsync(int academicYearId, string name, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(ClassGroup classGroup, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(ClassGroup classGroup, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<ClassGroupListItem>> SearchAsync(int? academicYearId, string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReplaceStreamsAsync(int classGroupId, int levelId, IReadOnlyList<int> streamIds, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeAnnualEnrollments(AnnualEnrollment annual) : IAnnualEnrollmentRepository
    {
        public Task<AnnualEnrollment?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<AnnualEnrollment?>(annual);
        public Task AddAsync(AnnualEnrollment enrollment, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AnnualEnrollment enrollment, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AnnualEnrollment?> GetActiveForStudentInYearAsync(int studentId, int academicYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<AnnualEnrollmentListItem>> GetForStudentAsync(int studentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveGroupEnrollmentsAsync(int annualEnrollmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<RolloverCandidateItem>> GetRolloverCandidatesAsync(int sourceYearId, int targetYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeAcademicYears : IAcademicYearRepository
    {
        public AcademicYear? Current { get; set; }
        public Task<AcademicYear?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<AcademicYear>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyWithNameAsync(string name, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyOverlappingAsync(DateOnly startDate, DateOnly endDate, int excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakePrices(long value) : ISubjectPriceRepository
    {
        public Task<long?> TryGetPriceAsync(int academicYearId, int levelId, int subjectId, CancellationToken cancellationToken = default) => Task.FromResult<long?>(value);
        public Task AddAsync(Domain.Pricing.SubjectPrice subjectPrice, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Domain.Pricing.SubjectPrice subjectPrice, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Domain.Pricing.SubjectPrice?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyExistsAsync(int academicYearId, int levelId, int subjectId, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<SubjectPriceListItem>> GetByYearAsync(int? academicYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
