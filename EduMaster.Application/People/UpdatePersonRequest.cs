



using EduMaster.Domain.Enums;

public sealed record UpdatePersonRequest(
    int Id, string? FirstName, string? LastName, string? FatherName,
    DateOnly? BirthDate, GenderType? Gender,
    string? Phone, string? Phone2, string? Email, string? Address,
    string? PhotoPath = null);