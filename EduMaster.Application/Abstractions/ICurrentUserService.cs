

namespace EduMaster.Application.Abstractions
{
    public interface ICurrentUserService
    {
        int? UserAccountId { get; }   // null = لم يسجَّل دخول بعد (أو عملية نظام)
        string? Username { get; }
    }
}
