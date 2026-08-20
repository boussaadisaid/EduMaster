

public sealed record PersonAccountInfo(
    int Id,
    string Username,
    bool IsActive,
    bool IsLockedOut,
    int? LockoutRemainingMinutes);