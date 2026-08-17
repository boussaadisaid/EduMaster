using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduMaster.Application.Abstractions
{
    public interface ICurrentUserService
    {
        int? UserAccountId { get; }   // null = لم يسجَّل دخول بعد (أو عملية نظام)
        string? Username { get; }
    }
}
