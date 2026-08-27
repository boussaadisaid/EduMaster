using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Reports;
using EduMaster.Application.Scheduling;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حصص الأفواج لفترة (6.4 — ق-2): «من بعد إلى» يُرفض قبل القراءة · التجميع بالفوج والعدّ بالحالة · دقائق المُقامة فقط (الملغاة لا دقائق لها D-90) · الترتيب بالمستوى ثم الفوج · الإلغاء يُرمى (D-64) · غير المتوقع عربي نظيف (D-24)</summary>
public sealed class GetGroupSessionsReportHandlerTests
{
    /// <summary>مزيّف الحصص لق-2 — GetByDateRangeAsync وحدها تعمل، والباقي لا يُستدعى في المختبَر</summary>
    private sealed class ClassSessionRepoFake : IClassSessionRepository
    {
        public IReadOnlyList<ClassSessionListItem> SessionsToReturn { get; set; } = new List<ClassSessionListItem>();
        public Exception? ToThrow { get; set; }
        public bool Called { get; private set; }

        public Task<IEnumerable<ClassSessionListItem>> GetByDateRangeAsync(DateTime from, DateTime toExclusive, int? classGroupId, CancellationToken cancellationToken = default)
        {
            Called = true;
            if (ToThrow is not null) throw ToThrow;
            return Task.FromResult(SessionsToReturn.AsEnumerable());
        }

        public Task AddAsync(ClassSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(ClassSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ClassSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyExistsAtAsync(int classGroupId, DateTime startsAt, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<DateTime>> GetSessionStartsAsync(int classGroupId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledBySlotAsync(int scheduleId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledByGroupAsync(int classGroupId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static ClassSessionListItem Session(int id, int groupId, string groupName, string level, SessionStatus status, int durationMinutes)
        => new(id, groupId, groupName, "الفيزياء", level, "أمينة", "بوعلام", null, null,
               new DateTime(2026, 8, 20, 10, 0, 0), durationMinutes, status, null, false, 20);

    private static (GetGroupSessionsReportHandler handler, ClassSessionRepoFake sessions) Build(
        IReadOnlyList<ClassSessionListItem>? sessions = null, Exception? toThrow = null)
    {
        var repo = new ClassSessionRepoFake { SessionsToReturn = sessions ?? new List<ClassSessionListItem>(), ToThrow = toThrow };
        return (new GetGroupSessionsReportHandler(repo, NullLogger<GetGroupSessionsReportHandler>.Instance), repo);
    }

    [Fact]
    public async Task FromAfterTo_ValidationFailure_RepositoryNotCalled()
    {
        var (handler, sessions) = Build();

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 1), null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(sessions.Called);   // التحقق قبل أي قراءة
    }

    [Fact]
    public async Task Success_GroupsByGroup_CountsAndHeldMinutes()   // الملغاة لا دقائق لها (D-90)
    {
        var (handler, _) = Build(new List<ClassSessionListItem>
        {
            Session(1, 10, "فيزياء أ", "1 ثانوي", SessionStatus.Held, 60),
            Session(2, 10, "فيزياء أ", "1 ثانوي", SessionStatus.Held, 90),
            Session(3, 10, "فيزياء أ", "1 ثانوي", SessionStatus.Scheduled, 60),
            Session(4, 10, "فيزياء أ", "1 ثانوي", SessionStatus.Cancelled, 120),   // ملغاة — لا تدخل الدقائق
            Session(5, 20, "فيزياء ب", "2 ثانوي", SessionStatus.Held, 30),
        });

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(new DateOnly(2026, 8, 1), report.From);
        Assert.Equal(new DateOnly(2026, 8, 27), report.To);
        Assert.Equal(2, report.Groups.Count);

        var first = report.Groups[0];   // الترتيب: المستوى ثم اسم الفوج
        Assert.Equal(10, first.ClassGroupId);
        Assert.Equal("فيزياء أ", first.GroupName);
        Assert.Equal("الفيزياء", first.SubjectName);
        Assert.Equal("1 ثانوي", first.LevelName);
        Assert.Equal("أمينة بوعلام", first.TeacherName);   // الترتيب D-41 من خاصية النوع القائم
        Assert.Equal(1, first.ScheduledCount);
        Assert.Equal(2, first.HeldCount);
        Assert.Equal(1, first.CancelledCount);
        Assert.Equal(150, first.HeldMinutes);
        Assert.Equal("2.5", first.HeldHoursText);

        Assert.Equal(1, report.ScheduledTotal);
        Assert.Equal(3, report.HeldTotal);
        Assert.Equal(1, report.CancelledTotal);
        Assert.Equal(180, report.HeldMinutesTotal);
        Assert.Equal("3", report.HeldHoursTotalText);
    }

    [Fact]
    public async Task EmptyPeriod_EmptyGroups()
    {
        var (handler, _) = Build();

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Groups);
        Assert.Equal(0, result.Value.HeldTotal);
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, _) = Build(toThrow: new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null));
    }

    [Fact]
    public async Task UnexpectedException_ArabicFailure()   // D-24
    {
        var (handler, _) = Build(toThrow: new InvalidOperationException("raw boom"));

        var result = await handler.ExecuteAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 27), null);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.DoesNotContain("boom", result.ErrorMessage!);
    }
}
