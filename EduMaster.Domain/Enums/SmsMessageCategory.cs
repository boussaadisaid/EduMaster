namespace EduMaster.Domain.Enums;

public enum SmsMessageCategory : byte
{
    DebtReminder = 1,
    PaymentConfirmation = 2,
    AbsenceNotification = 3,
    SessionBalanceNotification = 4,
    Administrative = 5,
    Custom = 6
}

public enum SmsMessageStatus : byte
{
    Pending = 1,
    Submitted = 2,
    Delivered = 3,
    Failed = 4,
    Cancelled = 5
}

public enum SmsBatchStatus : byte
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    PartialSuccess = 4,
    Failed = 5
}
