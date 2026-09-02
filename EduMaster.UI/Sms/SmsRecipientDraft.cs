using EduMaster.UI.Common.MVVM;

namespace EduMaster.UI.Sms;

public sealed class SmsRecipientDraft : BaseViewModel
{
    private bool _isSelected;
    public SmsRecipientDraft(int? personId, int? studentId, string fullName, string? guardianName, string phone,
        long? amountCentimes = null, int? remainingSessions = null, string? subjectName = null, string? dateText = null)
    {
        PersonId = personId;
        StudentId = studentId;
        FullName = fullName;
        GuardianName = guardianName;
        PhoneNumber = phone;
        AmountCentimes = amountCentimes;
        RemainingSessions = remainingSessions;
        SubjectName = subjectName;
        DateText = dateText;
        _isSelected = true;
    }

    public int? PersonId { get; }
    public int? StudentId { get; }
    public string FullName { get; }
    public string? GuardianName { get; }
    public string PhoneNumber { get; }
    public long? AmountCentimes { get; }
    public int? RemainingSessions { get; }
    public string? SubjectName { get; }
    public string? DateText { get; }
    public string AmountText => AmountCentimes.HasValue ? MoneyText(AmountCentimes.Value) : string.Empty;
    public string RemainingSessionsText => RemainingSessions?.ToString() ?? string.Empty;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private static string MoneyText(long centimes)
        => (centimes / 100m).ToString("0.##") + " دج";
}
