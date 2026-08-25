namespace EduMaster.Domain.Enums;

/// <summary>نوع الإيصال (D-108): قبض من الطالب · صرف إليه (استرجاع — واجهته في 4.3)</summary>
public enum PaymentKind : byte
{
    Receipt = 1,
    Refund = 2
}