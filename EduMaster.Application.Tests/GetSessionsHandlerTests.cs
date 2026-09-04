using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

public sealed class GetSessionsHandlerTests
{
    private sealed class FakeSessionRepository : IClassSessionRepository
    {
        public bool Called { get; private set; }
        public int? AcademicYearIdReceived { get; private set; }
        public System.Collections.Generic.IEnumerable<ClassSessionListItem> Items { get; set; } = Array.Empty<ClassSessionListItem>();

        public Task<IEnumerable<ClassSessionListItem>> GetByDateRangeAsync(DateTime from, DateTime toExclusive, int? classGroupId, int? academicYearId = null, CancellationToken cancellationToken = default)
        {
            Called = true;
            AcademicYearIdReceived = academicYearId;
            return Task.FromResult(Items);
        }

        public Task AddAsync(EduMaster.Domain.Scheduling.ClassSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateAsync(EduMaster.Domain.Scheduling.ClassSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EduMaster.Domain.Scheduling.ClassSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<EduMaster.Domain.Scheduling.ClassSession?> GetByIdForAcademicYearAsync(int id, int academicYearId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AnyExistsAtAsync(int classGroupId, DateTime startsAt, int? excludeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<DateTime>> GetSessionStartsAsync(int classGroupId, DateTime from, DateTime toExclusive, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledBySlotAsync(int scheduleId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CancelFutureScheduledByGroupAsync(int classGroupId, DateTime localNow, DateTime utcNow, int? updatedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task OperationalQuery_CarriesCurrentAcademicYearId()
    {
        var repo = new FakeSessionRepository();
        var handler = new GetSessionsHandler(repo, NullLogger<GetSessionsHandler>.Instance);

        var result = await handler.ExecuteAsync(
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 3), null, 7);

        Assert.True(result.IsSuccess);
        Assert.True(repo.Called);
        Assert.Equal(7, repo.AcademicYearIdReceived);
    }

    [Fact]
    public async Task InvalidDateRange_DoesNotCallRepository()
    {
        var repo = new FakeSessionRepository();
        var handler = new GetSessionsHandler(repo, NullLogger<GetSessionsHandler>.Instance);

        var result = await handler.ExecuteAsync(
            new DateTime(2026, 9, 4), new DateTime(2026, 9, 3), null, 7);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.False(repo.Called);
    }
}
