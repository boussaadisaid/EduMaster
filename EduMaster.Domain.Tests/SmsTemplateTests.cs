using EduMaster.Domain.Common;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Sms;
using System;
using Xunit;

namespace EduMaster.Domain.Tests;

public sealed class SmsTemplateTests
{
    [Fact]
    public void Create_WithBlankName_Throws()
    {
        Assert.Throws<DomainException>(() => SmsTemplate.Create(" ", SmsMessageCategory.Administrative, "Hi", DateTime.UtcNow, 1));
    }

    [Fact]
    public void Create_WithBlankBody_Throws()
    {
        Assert.Throws<DomainException>(() => SmsTemplate.Create("Test", SmsMessageCategory.Administrative, " ", DateTime.UtcNow, 1));
    }

    [Fact]
    public void Create_StartsActive()
    {
        var item = SmsTemplate.Create("Test", SmsMessageCategory.Administrative, "Hi", DateTime.UtcNow, 1);
        Assert.True(item.IsActive);
    }
}
