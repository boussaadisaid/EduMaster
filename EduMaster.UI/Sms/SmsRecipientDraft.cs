using EduMaster.UI.Common.MVVM;

namespace EduMaster.UI.Sms;

public sealed record SmsPhoneOption(string Label, string Number)
{
    public string DisplayText => $"{Label}: {Number}";
}

public sealed class SmsRecipientDraft : BaseViewModel
{
    private bool _isSelected;
    private SmsPhoneOption _selectedPhone;

    public SmsRecipientDraft(int? personId, int? studentId, string fullName, string? guardianName, string phone,
        long? amountCentimes = null, int? remainingSessions = null, string? subjectName = null, string? dateText = null)
        : this(
            personId,
            studentId,
            fullName,
            guardianName,
            new[] { new SmsPhoneOption("الهاتف", phone) },
            amountCentimes,
            remainingSessions,
            subjectName,
            dateText)
    {
    }

    public SmsRecipientDraft(int? personId, int? studentId, string fullName, string? guardianName,
        IEnumerable<SmsPhoneOption> phoneOptions,
        long? amountCentimes = null, int? remainingSessions = null, string? subjectName = null, string? dateText = null)
    {
        PersonId = personId;
        StudentId = studentId;
        FullName = fullName;
        GuardianName = guardianName;
        PhoneOptions = phoneOptions
            .Where(x => !string.IsNullOrWhiteSpace(x.Number))
            .GroupBy(x => x.Number, StringComparer.Ordinal)
            .Select(x => x.First())
            .ToList();

        if (PhoneOptions.Count == 0)
            throw new ArgumentException("At least one valid phone option is required.", nameof(phoneOptions));

        _selectedPhone = PhoneOptions[0];
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
    public IReadOnlyList<SmsPhoneOption> PhoneOptions { get; }
    public SmsPhoneOption SelectedPhone
    {
        get => _selectedPhone;
        set
        {
            if (value is null || !PhoneOptions.Contains(value) || ReferenceEquals(_selectedPhone, value))
                return;

            if (SetProperty(ref _selectedPhone, value))
                OnPropertyChanged(nameof(PhoneNumber));
        }
    }

    public string PhoneNumber => SelectedPhone.Number;
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
