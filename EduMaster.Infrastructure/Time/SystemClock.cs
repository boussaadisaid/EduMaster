using EduMaster.Application.Abstractions;

namespace EduMaster.Infrastructure.Time
{
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    }
}
