namespace EduMaster.Domain.Enums;

/// <summary>نوع المستحق (D-103): يتولد ذرّياً من مصدره — لا مستحقات يدوية في V1</summary>
public enum ChargeKind : byte
{
    RegistrationFee = 1,
    SessionBundle = 2
}