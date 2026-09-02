using System.Globalization;

namespace EduMaster.Application.Sms;

public sealed record SmsTemplateRenderData(
    string? StudentName,
    string? ParentName,
    long? AmountCentimes,
    long? RemainingAmountCentimes,
    int? RemainingSessions,
    string? SubjectName,
    string? DateText,
    string SchoolName,
    string? Message);

public static class SmsTemplateRenderer
{
    public static string Render(string template, SmsTemplateRenderData data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        return template
            .Replace("{StudentName}", data.StudentName ?? string.Empty, StringComparison.Ordinal)
            .Replace("{ParentName}", data.ParentName ?? string.Empty, StringComparison.Ordinal)
            .Replace("{Amount}", FormatAmount(data.AmountCentimes), StringComparison.Ordinal)
            .Replace("{RemainingAmount}", FormatAmount(data.RemainingAmountCentimes), StringComparison.Ordinal)
            .Replace("{RemainingSessions}", data.RemainingSessions?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, StringComparison.Ordinal)
            .Replace("{SubjectName}", data.SubjectName ?? string.Empty, StringComparison.Ordinal)
            .Replace("{Date}", data.DateText ?? string.Empty, StringComparison.Ordinal)
            .Replace("{SchoolName}", data.SchoolName ?? string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string FormatAmount(long? centimes)
        => centimes.HasValue ? (centimes.Value / 100m).ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
}
