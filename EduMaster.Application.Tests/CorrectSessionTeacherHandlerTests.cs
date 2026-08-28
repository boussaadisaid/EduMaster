using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using EduMaster.Application.Teachers;
using EduMaster.Application.Tests.Fakes;
using EduMaster.Domain.Scheduling;
using EduMaster.Domain.Teachers;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>تصحيح لقطة الأستاذ (6.6-ص-ب): نجاح يكتب اللقطة مختومة ويحفظها في معاملة · المُقامة فقط · اللقطة القائمة لا تُعاد كتابتها أبداً · غياب الحصة/الأستاذ · غير المتوقع عربي بتراجع</summary>
public class CorrectSessionTeacherHandlerTests
{
    private sealed class SessionsFake : IClassSessionRepository
    {
        public ClassSession? ToReturn { get; set; }
        public Exception? ToThrow { get; set; }
        public ClassSession? Updated { get; private set; }

        public Task<ClassSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => ToThrow is not null ? throw ToThrow : Task.FromResult(ToReturn);
        public Task UpdateAsync(ClassSession session, CancellationToken cancellationToken = default)
        { Updated = session; return Task.CompletedTask; }

        public Task AddAsync(ClassSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<ClassSessionListItem>> GetByDateRangeAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyExistsAtAsync(int classGroupId, DateTime startsAt, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<DateTime>> GetSessionStartsAsync(int classGroupId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledBySlotAsync(int scheduleId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledByGroupAsync(int classGroupId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TeachersFake : ITeacherRepository
    {
        public Teacher? ToReturn { get; set; }

        public Task<Teacher?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(ToReturn);

        public Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(Teacher teacher, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyActiveForPersonAsync(int personId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TeacherListItem>> SearchAsync(string? normalizedTerm, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SoftDeleteAsync(int id, DateTime deletedAtUtc, int? deletedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static readonly DateTime FixedUtc = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);   // مرآة FakeClock الافتراضي

    private static ClassSession HeldSessionWithNullSnapshot()
    {
        var session = ClassSession.Create(5, null, new DateTime(2026, 8, 20, 18, 0, 0), 60, null, FixedUtc, 1);
        session.SetId(10);
        session.MarkHeld(null, FixedUtc, 1);   // أُقيمت بلا أستاذ مسند لحظتها — اللقطة فارغة
        return session;
    }

    private static Teacher SomeTeacher(int id = 3)
    {
        var teacher = Teacher.Create(7, null, null, FixedUtc, 1);
        teacher.SetId(id);
        return teacher;
    }

    private static (CorrectSessionTeacherHandler handler, SessionsFake sessions, FakeUnitOfWork uow) Build(
        ClassSession? session, Teacher? teacher)
    {
        var sessions = new SessionsFake { ToReturn = session };
        var uow = new FakeUnitOfWork();
        return (new CorrectSessionTeacherHandler(sessions, new TeachersFake { ToReturn = teacher },
            new FakeClock(), new FakeCurrentUserService(), uow,
            NullLogger<CorrectSessionTeacherHandler>.Instance), sessions, uow);
    }

    [Fact]
    public async Task Success_SetsSnapshotWithAudit_AndPersistsInTransaction()
    {
        var session = HeldSessionWithNullSnapshot();
        var (handler, sessions, uow) = Build(session, SomeTeacher());

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(10, 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, session.TeacherId);                     // اللقطة كُتبت
        Assert.Equal(FixedUtc, session.UpdatedAtUtc);           // مختومة باللحظة
        Assert.Equal(1, session.UpdatedByUserId);               // وبالمستخدم (FakeCurrentUserService الافتراضي)
        Assert.Same(session, sessions.Updated);                 // وحُفظت
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
    }

    [Fact]
    public async Task ScheduledSession_Rejected_MustaBeHeld()
    {
        var session = ClassSession.Create(5, null, new DateTime(2026, 8, 20, 18, 0, 0), 60, null, FixedUtc, 1);
        session.SetId(10);   // مجدولة — لم تُقَم
        var (handler, sessions, uow) = Build(session, SomeTeacher());

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(10, 3));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("تصحيح اللقطة على حصة مُقامة فقط.", result.ErrorMessage);
        Assert.Null(sessions.Updated);
        Assert.Equal(1, uow.RolledBackCount);
    }

    [Fact]
    public async Task ExistingSnapshot_NeverRewritten()
    {
        var session = HeldSessionWithNullSnapshot();
        // لقطة قائمة: أُعيدت الإقامة بأستاذ (الكيان: المُقامة خاملة — نبنيها مباشرة)
        var withSnapshot = ClassSession.Create(5, null, new DateTime(2026, 8, 20, 18, 0, 0), 60, null, FixedUtc, 1);
        withSnapshot.SetId(10);
        withSnapshot.MarkHeld(3, FixedUtc, 1);
        var (handler, sessions, _) = Build(withSnapshot, SomeTeacher(9));

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(10, 9));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("لهذه الحصة لقطة أستاذ قائمة — اللقطات لا تُعاد كتابتها.", result.ErrorMessage);
        Assert.Equal(3, withSnapshot.TeacherId);                // بقيت كما هي
        Assert.Null(sessions.Updated);
    }

    [Fact]
    public async Task MissingSession_NotFound_WithoutTransaction()
    {
        var (handler, _, uow) = Build(null, SomeTeacher());

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(99, 3));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task MissingTeacher_Validation_WithoutTransaction()
    {
        var (handler, _, uow) = Build(HeldSessionWithNullSnapshot(), null);

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(10, 77));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal("الأستاذ المحدد غير موجود.", result.ErrorMessage);
        Assert.Equal(0, uow.BeganCount);
    }

    [Fact]
    public async Task Unexpected_FailsArabicGeneric_WithRollback()
    {
        var sessions = new SessionsFake { ToThrow = new InvalidOperationException("boom") };
        var uow = new FakeUnitOfWork();
        var handler = new CorrectSessionTeacherHandler(sessions, new TeachersFake(),
            new FakeClock(), new FakeCurrentUserService(), uow, NullLogger<CorrectSessionTeacherHandler>.Instance);

        var result = await handler.ExecuteAsync(new CorrectSessionTeacherRequest(10, 3));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Equal("حدث خطأ غير متوقع أثناء تصحيح لقطة الأستاذ.", result.ErrorMessage);
        Assert.Equal(1, uow.RolledBackCount);
    }
}
