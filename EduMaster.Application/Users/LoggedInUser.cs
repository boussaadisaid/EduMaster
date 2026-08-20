namespace EduMaster.Application.Users;

public sealed record LoggedInUser(int UserAccountId, string Username, int PersonId, bool MustChangePassword);