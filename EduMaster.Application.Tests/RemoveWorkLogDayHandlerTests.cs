using EduMaster.Application.Common;
using EduMaster.Application.Payroll;
using EduMaster.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>حذف يوم عمل (D-115): الموجود يُحذف في Commit · المفقود NotFound بلا Commit</summary>
public sealed class RemoveWorkLogDayHandlerTests
{
    private static (RemoveWorkLogDayHandler handler, FakeEmployeeWorkLogRepository workLog, FakeUnitOfWork uow) Build(int deleteResult = 1)
    {
        var workLog = new FakeEmployeeWorkLogRepository { DeleteResult = deleteResult };
        var uow = new FakeUnitOfWork();
        var handler = new RemoveWorkLogDayHandler(workLog, uow, NullLogger<RemoveWorkLogDayHandler>.Instance);
        return (handler, workLog, uow);
    }

    [Fact]
    public async Task Found_Deletes_AndCommits()
    {
        var (handler, workLog, uow) = Build();

        var result = await handler.ExecuteAsync(new RemoveWorkLogDayRequest(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(5, Assert.Single(workLog.DeletedIds));
        Assert.Equal(1, uow.BeganCount);
        Assert.Equal(1, uow.CommittedCount);
        Assert.Equal(0, uow.RolledBackCount);
    }

    [Fact]
    public async Task Missing_NotFound_RollsBack()
    {
        var (handler, workLog, uow) = Build(deleteResult: 0);

        var result = await handler.ExecuteAsync(new RemoveWorkLogDayRequest(99));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.ErrorType);   // «ربما حُذف مسبقاً»
        Assert.Equal(99, Assert.Single(workLog.DeletedIds));   // المحاولة سُجّلت — والقاعدة لم تجد صفاً
        Assert.Equal(1, uow.RolledBackCount);
        Assert.Equal(0, uow.CommittedCount);
    }
}