using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>سجل أيام العمل — D-112 مطبَّق: WorkDate تبقى DateOnly في الدومين وتتحوّل DateTime عند معاملات Dapper قراءةً وكتابةً</summary>
public sealed class EmployeeWorkLogRepository : IEmployeeWorkLogRepository
{
    private readonly IAdoDbSession _session;

    public EmployeeWorkLogRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: بنفس ترتيب أعمدة SELECT في GetForEmployeeAsync — WorkDate تُقرأ DateTime من عمود DATE
    private sealed record WorkLogRow(
        int Id,
        int EmployeeId,
        DateTime WorkDate,
        string? Note);

    public async Task AddAsync(WorkLogEntry entry, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO EmployeeWorkLog (EmployeeId, WorkDate, Note, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@EmployeeId, @WorkDate, @Note, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                entry.EmployeeId,
                WorkDate = entry.WorkDate.ToDateTime(TimeOnly.MinValue),   // D-112: DateOnly تتحوّل عند الحدود
                entry.Note,
                entry.CreatedAtUtc,
                entry.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        entry.SetId(newId);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // حذف فيزيائي مقصود: سجل تشغيلي غير مالي «كتابة فقط» — التصحيح = حذف اليوم وإعادة تسجيله (D-115) · يعيد عدد الصفوف المحذوفة
        return await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM EmployeeWorkLog WHERE Id = @Id;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<WorkLogItem>> GetForEmployeeAsync(int employeeId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, EmployeeId, WorkDate, Note
FROM EmployeeWorkLog
WHERE EmployeeId = @EmployeeId
  AND (@From IS NULL OR WorkDate >= @From)
  AND (@To IS NULL OR WorkDate <= @To)
ORDER BY WorkDate DESC, Id DESC;";

        var rows = await connection.QueryAsync<WorkLogRow>(
            new CommandDefinition(sql, new
            {
                EmployeeId = employeeId,
                From = from?.ToDateTime(TimeOnly.MinValue),   // D-112: التحويل عند الحدود لا في الدومين
                To = to?.ToDateTime(TimeOnly.MinValue)
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return rows.Select(row => new WorkLogItem(row.Id, row.EmployeeId, row.WorkDate, row.Note)).ToList();
    }
}