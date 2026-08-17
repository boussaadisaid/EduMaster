
namespace EduMaster.Application.Common;

/// <summary>نوع الخطأ — تقرأه الواجهة لتقرر كيف تعرضه (رسالة تحت حقل / نافذة / تحديث قائمة)</summary>
public enum ErrorType
{
    None = 0,
    Validation,     // خطأ إدخال من المستخدم
    NotFound,       // الكيان غير موجود
    Conflict,       // تعارض (مثال: اسم مكرر، تسجيل مزدوج)
    BusinessRule,   // قاعدة عمل مكسورة
    Unexpected      // خلل تقني
}

public class OperationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ErrorType ErrorType { get; }

    private OperationResult(bool isSuccess, string? errorMessage, ErrorType errorType)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    public static OperationResult Success()
        => new(true, null, ErrorType.None);

    public static OperationResult Failure(string errorMessage, ErrorType errorType = ErrorType.BusinessRule)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required for a failed result.", nameof(errorMessage));
        if (errorType == ErrorType.None)
            throw new ArgumentException("Failure cannot have ErrorType.None.", nameof(errorType));
        return new(false, errorMessage, errorType);
    }
}

public class OperationResult<T>
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public ErrorType ErrorType { get; }
    public T? Value { get; }

    private OperationResult(bool isSuccess, T? value, string? errorMessage, ErrorType errorType)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
    }

    public static OperationResult<T> Success(T value)
        => new(true, value, null, ErrorType.None);

    public static OperationResult<T> Failure(string errorMessage, ErrorType errorType = ErrorType.BusinessRule)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required for a failed result.", nameof(errorMessage));
        if (errorType == ErrorType.None)
            throw new ArgumentException("Failure cannot have ErrorType.None.", nameof(errorType));
        return new(false, default, errorMessage, errorType);
    }
}
