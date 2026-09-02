using EduMaster.Application.Sms;
using Xunit;

namespace EduMaster.Application.Tests;

public sealed class SmsTemplateRendererTests
{
    [Fact]
    public void Render_ReplacesKnownPlaceholders()
    {
        var result = SmsTemplateRenderer.Render(
            "السلام عليكم {ParentName}، {StudentName} عليه {Amount} دج، تبقى {RemainingSessions} حصة — {SubjectName} — {Date} — {SchoolName}",
            new SmsTemplateRenderData("محمد", "السيد أحمد", 350000, 120000, 2, "الرياضيات", "02/09/2026", "نادي المنار", null));

        Assert.Contains("السيد أحمد", result);
        Assert.Contains("محمد", result);
        Assert.Contains("3500", result);
        Assert.Contains("2", result);
        Assert.Contains("الرياضيات", result);
        Assert.Contains("02/09/2026", result);
        Assert.Contains("نادي المنار", result);
        Assert.DoesNotContain("{StudentName}", result);
        Assert.DoesNotContain("{SchoolName}", result);
    }

    [Fact]
    public void Render_MissingOptionalValuesBecomeEmpty()
    {
        var result = SmsTemplateRenderer.Render(
            "{StudentName}|{ParentName}|{Amount}|{RemainingSessions}|{SubjectName}|{Date}|{SchoolName}",
            new SmsTemplateRenderData("محمد", null, null, null, null, null, null, "EduMaster", null));

        Assert.Equal("محمد||||||EduMaster", result);
    }
}
