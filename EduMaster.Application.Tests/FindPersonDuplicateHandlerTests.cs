using EduMaster.Application.Common;
using EduMaster.Application.People;
using EduMaster.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

/// <summary>فحص تكرار الأشخاص (6.6 — ز-2): تطابق ← المطابِق · لا تطابق ← null · التطبيع بترتيب تركيب الكيان حرفاً (الأول/اللقب/الأب — الكيان سطر 141) · اسم فارغ يتخطّى الاستعلام · الإلغاء D-64 · غير المتوقع عربي عام</summary>
public class FindPersonDuplicateHandlerTests
{
    private static (FindPersonDuplicateHandler handler, FakePersonRepository persons) Build()
    {
        var persons = new FakePersonRepository();
        return (new FindPersonDuplicateHandler(persons, NullLogger<FindPersonDuplicateHandler>.Instance), persons);
    }

    [Fact]
    public async Task MatchFound_ReturnsIdAndFullName()
    {
        var (handler, persons) = Build();
        persons.MatchToReturn = new PersonDuplicateRaw(7, "احمد علي");

        var result = await handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "علي", null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(7, result.Value!.Id);
        Assert.Equal("احمد علي", result.Value.FullName);
    }

    [Fact]
    public async Task NoMatch_ReturnsNull()
    {
        var (handler, _) = Build();

        var result = await handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "علي", null));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Normalization_MatchesEntityComposition_Exactly()   // الأول/اللقب/الأب — لا ترتيب المعاملات
    {
        var (handler, persons) = Build();

        await handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "إبراهيم", "مُحمد"));

        Assert.Equal("احمد ابراهيم محمد", persons.LastNormalizedNameReceived);
    }

    [Fact]
    public async Task NullFather_ComposesWithoutHim()
    {
        var (handler, persons) = Build();

        await handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "إبراهيم", null));

        Assert.Equal("احمد ابراهيم", persons.LastNormalizedNameReceived);
    }

    [Fact]
    public async Task BlankName_SucceedsWithoutQuery()
    {
        var (handler, persons) = Build();

        var result = await handler.ExecuteAsync(new FindPersonDuplicateRequest(" ", " ", null));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(persons.LastNormalizedNameReceived);   // لم يُستعلم أصلاً
    }

    [Fact]
    public async Task Cancellation_Propagates()   // D-64
    {
        var (handler, persons) = Build();
        persons.ToThrowOnDuplicateCheck = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "علي", null)));
    }

    [Fact]
    public async Task Unexpected_FailsWithArabicGeneric()
    {
        var (handler, persons) = Build();
        persons.ToThrowOnDuplicateCheck = new InvalidOperationException("boom");

        var result = await handler.ExecuteAsync(new FindPersonDuplicateRequest("أحمد", "علي", null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unexpected, result.ErrorType);
        Assert.Equal("حدث خطأ غير متوقع أثناء فحص تكرار الاسم.", result.ErrorMessage);
    }
}
