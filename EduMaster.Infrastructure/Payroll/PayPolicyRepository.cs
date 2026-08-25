using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Payroll;
using EduMaster.Domain.Payroll;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Payroll;

/// <summary>مستودع سياسات الأجر — التعدادات TINYINT تُقرأ byte وتُصبغ عند التجسيد (نمط PaymentRepository) · لا IsDeleted في PayPolicies (الحذف = تعطيل)</summary>
public sealed class PayPolicyRepository : IPayPolicyRepository
{
    private readonly IAdoDbSession _session;

    public PayPolicyRepository(IAdoDbSession session) => _session = session;

    // ⚠ D-81: بترتيب أعمدة SELECT حرفياً في قراءات الكيان الأربع أدناه
    private sealed record PayPolicyRow(
        int Id,
        byte PayeeKind,
        int? TeacherId,
        int? EmployeeId,
        int? ClassGroupId,
        byte Kind,
        long RateCentimes,
        decimal? Percentage,
        bool CountsUnjustifiedAbsent,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    public async Task AddAsync(PayPolicy policy, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO PayPolicies (PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage,
    CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@PayeeKind, @TeacherId, @EmployeeId, @ClassGroupId, @Kind, @RateCentimes, @Percentage,
    @CountsUnjustifiedAbsent, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                PayeeKind = (byte)policy.PayeeKind,
                policy.TeacherId,
                policy.EmployeeId,
                policy.ClassGroupId,
                Kind = (byte)policy.Kind,
                policy.RateCentimes,
                policy.Percentage,
                policy.CountsUnjustifiedAbsent,
                policy.IsActive,
                policy.CreatedAtUtc,
                policy.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        policy.SetId(newId);
    }

    public async Task UpdateAsync(PayPolicy policy, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // التعديل على النوع/القيمة/العلم/الحالة فقط — الهوية (المستفيد/الفوج) ثابتة (روح D-61)
        const string sql = @"
UPDATE PayPolicies
SET Kind                    = @Kind,
    RateCentimes            = @RateCentimes,
    Percentage              = @Percentage,
    CountsUnjustifiedAbsent = @CountsUnjustifiedAbsent,
    IsActive                = @IsActive,
    UpdatedAtUtc            = @UpdatedAtUtc,
    UpdatedByUserId         = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                Kind = (byte)policy.Kind,
                policy.RateCentimes,
                policy.Percentage,
                policy.CountsUnjustifiedAbsent,
                policy.IsActive,
                policy.UpdatedAtUtc,
                policy.UpdatedByUserId,
                policy.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"Pay policy {policy.Id} was not found for update.");
    }

    public async Task<PayPolicy?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage,
       CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM PayPolicies
WHERE Id = @Id;";

        var row = await connection.QuerySingleOrDefaultAsync<PayPolicyRow>(
            new CommandDefinition(sql, new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    /// <summary>يطابق الفهرس المفلتر UX_PayPolicies_Teacher_Default_Active (سكربت 015)</summary>
    public async Task<PayPolicy?> GetActiveDefaultForTeacherAsync(int teacherId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage,
       CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM PayPolicies
WHERE PayeeKind = 1 AND TeacherId = @TeacherId AND ClassGroupId IS NULL AND IsActive = 1;";

        var row = await connection.QuerySingleOrDefaultAsync<PayPolicyRow>(
            new CommandDefinition(sql, new { TeacherId = teacherId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    /// <summary>يطابق الفهرس المفلتر UX_PayPolicies_Teacher_Group_Active (سكربت 015)</summary>
    public async Task<PayPolicy?> GetActiveOverrideAsync(int teacherId, int classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage,
       CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM PayPolicies
WHERE PayeeKind = 1 AND TeacherId = @TeacherId AND ClassGroupId = @ClassGroupId AND IsActive = 1;";

        var row = await connection.QuerySingleOrDefaultAsync<PayPolicyRow>(
            new CommandDefinition(sql, new { TeacherId = teacherId, ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    /// <summary>يطابق الفهرس المفلتر UX_PayPolicies_Employee_Active (سكربت 015)</summary>
    public async Task<PayPolicy?> GetActiveForEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
SELECT Id, PayeeKind, TeacherId, EmployeeId, ClassGroupId, Kind, RateCentimes, Percentage,
       CountsUnjustifiedAbsent, IsActive, CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM PayPolicies
WHERE PayeeKind = 2 AND EmployeeId = @EmployeeId AND IsActive = 1;";

        var row = await connection.QuerySingleOrDefaultAsync<PayPolicyRow>(
            new CommandDefinition(sql, new { EmployeeId = employeeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<IReadOnlyList<PayPolicyItem>> ListAsync(PayeeKind? payeeKind, int? payeeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // قراءة مسطّحة (D-40): اسم المستفيد حسب نوعه — أستاذ عبر Teachers ← Persons · موظف عبر Employees ← Persons — بترتيب الاسم D-41
        const string sql = @"
SELECT pol.Id, pol.PayeeKind, pol.TeacherId, pol.EmployeeId,
       CASE WHEN pol.PayeeKind = 1
            THEN CONCAT_WS(N' ', pt.FirstName, pt.LastName, pt.FatherName)
            ELSE CONCAT_WS(N' ', pe.FirstName, pe.LastName, pe.FatherName) END AS PayeeName,
       pol.ClassGroupId, cg.Name AS ClassGroupName,
       pol.Kind, pol.RateCentimes, pol.Percentage, pol.CountsUnjustifiedAbsent, pol.IsActive
FROM PayPolicies pol
LEFT JOIN Teachers t ON t.Id = pol.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons pt ON pt.Id = t.PersonId AND pt.IsDeleted = 0
LEFT JOIN Employees e ON e.Id = pol.EmployeeId AND e.IsDeleted = 0
LEFT JOIN Persons pe ON pe.Id = e.PersonId AND pe.IsDeleted = 0
LEFT JOIN ClassGroups cg ON cg.Id = pol.ClassGroupId
WHERE (@PayeeKind IS NULL OR pol.PayeeKind = @PayeeKind)
  AND (@PayeeId IS NULL
       OR (pol.PayeeKind = 1 AND pol.TeacherId = @PayeeId)
       OR (pol.PayeeKind = 2 AND pol.EmployeeId = @PayeeId))
ORDER BY pol.IsActive DESC, pol.Id DESC;";

        var rows = await connection.QueryAsync<PayPolicyListRow>(
            new CommandDefinition(sql, new
            {
                PayeeKind = (byte?)payeeKind,
                PayeeId = payeeId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        return rows.Select(row => new PayPolicyItem(
            row.Id,
            (PayeeKind)row.PayeeKind,
            row.TeacherId,
            row.EmployeeId,
            row.PayeeName,
            row.ClassGroupId,
            row.ClassGroupName,
            (PayPolicyKind)row.Kind,
            row.RateCentimes,
            row.Percentage,
            row.CountsUnjustifiedAbsent,
            row.IsActive)).ToList();
    }

    // ⚠ D-81: بترتيب أعمدة SELECT في ListAsync حرفياً
    private sealed record PayPolicyListRow(
        int Id,
        byte PayeeKind,
        int? TeacherId,
        int? EmployeeId,
        string PayeeName,
        int? ClassGroupId,
        string? ClassGroupName,
        byte Kind,
        long RateCentimes,
        decimal? Percentage,
        bool CountsUnjustifiedAbsent,
        bool IsActive);

    private static PayPolicy MapToDomain(PayPolicyRow row) =>
        PayPolicy.Load(
            id: row.Id,
            payeeKind: (PayeeKind)row.PayeeKind,
            teacherId: row.TeacherId,
            employeeId: row.EmployeeId,
            classGroupId: row.ClassGroupId,
            kind: (PayPolicyKind)row.Kind,
            rateCentimes: row.RateCentimes,
            percentage: row.Percentage,
            countsUnjustifiedAbsent: row.CountsUnjustifiedAbsent,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}