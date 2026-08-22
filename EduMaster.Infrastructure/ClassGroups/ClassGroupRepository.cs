using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.ClassGroups;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.ClassGroups;

public sealed class ClassGroupRepository : IClassGroupRepository
{
    private readonly IAdoDbSession _session;

    public ClassGroupRepository(IAdoDbSession session) => _session = session;

    private sealed record ClassGroupRow(
        int Id,
        int AcademicYearId,
        int LevelId,
        int SubjectId,
        int? TeacherId,
        int? RoomId,
        string Name,
        string NameNormalized,
        int? Capacity,
        bool IsActive,
        DateTime CreatedAtUtc,
        int? CreatedByUserId,
        DateTime? UpdatedAtUtc,
        int? UpdatedByUserId);

    // ⚠ قاعدة D-81: ترتيب خصائص السجل = ترتيب أعمدة الـSELECT حرفياً (مطابقة Dapper اسمية-موضعية)
    private sealed record ClassGroupListRow(
        int Id,
        int AcademicYearId,
        string AcademicYearName,
        int LevelId,
        string LevelName,
        int SubjectId,
        string SubjectName,
        int? TeacherId,
        string? TeacherFirstName,
        string? TeacherLastName,
        string? TeacherFatherName,
        int? RoomId,
        string? RoomName,
        string Name,
        int? Capacity,
        bool IsActive,
        string? StreamsText,
        int EnrolledCount);

    private const string SelectColumns = @"
SELECT Id, AcademicYearId, LevelId, SubjectId, TeacherId, RoomId, Name, NameNormalized, Capacity, IsActive,
       CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId
FROM ClassGroups";

    public async Task AddAsync(Domain.ClassGroups.ClassGroup classGroup, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
INSERT INTO ClassGroups (AcademicYearId, LevelId, SubjectId, TeacherId, RoomId, Name, NameNormalized, Capacity, IsActive, CreatedAtUtc, CreatedByUserId)
OUTPUT INSERTED.Id
VALUES (@AcademicYearId, @LevelId, @SubjectId, @TeacherId, @RoomId, @Name, @NameNormalized, @Capacity, @IsActive, @CreatedAtUtc, @CreatedByUserId);";

        var newId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new
            {
                classGroup.AcademicYearId,
                classGroup.LevelId,
                classGroup.SubjectId,
                classGroup.TeacherId,
                classGroup.RoomId,
                classGroup.Name,
                classGroup.NameNormalized,
                classGroup.Capacity,
                classGroup.IsActive,
                classGroup.CreatedAtUtc,
                classGroup.CreatedByUserId
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        classGroup.SetId(newId);
    }

    public async Task UpdateAsync(Domain.ClassGroups.ClassGroup classGroup, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string sql = @"
UPDATE ClassGroups
SET Name            = @Name,
    NameNormalized  = @NameNormalized,
    TeacherId       = @TeacherId,
    RoomId          = @RoomId,
    Capacity        = @Capacity,
    IsActive        = @IsActive,
    UpdatedAtUtc    = @UpdatedAtUtc,
    UpdatedByUserId = @UpdatedByUserId
WHERE Id = @Id;";

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                classGroup.Name,
                classGroup.NameNormalized,
                classGroup.TeacherId,
                classGroup.RoomId,
                classGroup.Capacity,
                classGroup.IsActive,
                classGroup.UpdatedAtUtc,
                classGroup.UpdatedByUserId,
                classGroup.Id
            },
            transaction: _session.CurrentTransaction,
            cancellationToken: cancellationToken));

        if (affected == 0)
            throw new InvalidOperationException($"ClassGroup {classGroup.Id} was not found for update.");
    }

    public async Task<Domain.ClassGroups.ClassGroup?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ClassGroupRow>(
            new CommandDefinition($"{SelectColumns} WHERE Id = @Id;", new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return row is null ? null : MapToDomain(row);
    }

    public async Task<bool> AnyWithNameInYearAsync(int academicYearId, string name, int? excludeId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM ClassGroups
                  WHERE AcademicYearId = @AcademicYearId AND Name = @Name AND (@ExcludeId IS NULL OR Id <> @ExcludeId);",
                new { AcademicYearId = academicYearId, Name = name, ExcludeId = excludeId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<IEnumerable<ClassGroupListItem>> SearchAsync(int? academicYearId, string? normalizedTerm, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // نموذج قراءة مسطّح (D-40) — الشعب تُجمَّع نصاً والفارغ يعرض «كل الشعب» (D-48) · عداد النشطين (D-80)
        const string sql = @"
SELECT cg.Id, cg.AcademicYearId, ay.Name AS AcademicYearName,
       cg.LevelId, l.Name AS LevelName,
       cg.SubjectId, sb.Name AS SubjectName,
       cg.TeacherId, tp.FirstName AS TeacherFirstName, tp.LastName AS TeacherLastName, tp.FatherName AS TeacherFatherName,
       cg.RoomId, r.Name AS RoomName,
       cg.Name, cg.Capacity, cg.IsActive,
       (SELECT STRING_AGG(s.Name, N'، ')
        FROM ClassGroupStreams cgs
        JOIN Streams s ON s.Id = cgs.StreamId
        WHERE cgs.ClassGroupId = cg.Id) AS StreamsText,
       (SELECT COUNT(*) FROM ClassGroupEnrollments e WHERE e.ClassGroupId = cg.Id AND e.Status = 1) AS EnrolledCount
FROM ClassGroups cg
JOIN AcademicYears ay ON ay.Id = cg.AcademicYearId
JOIN Levels l ON l.Id = cg.LevelId
JOIN Subjects sb ON sb.Id = cg.SubjectId
LEFT JOIN Teachers t ON t.Id = cg.TeacherId AND t.IsDeleted = 0
LEFT JOIN Persons tp ON tp.Id = t.PersonId AND tp.IsDeleted = 0
LEFT JOIN Rooms r ON r.Id = cg.RoomId
WHERE (@YearId IS NULL OR cg.AcademicYearId = @YearId)
  AND (@Term IS NULL
       OR cg.NameNormalized LIKE '%' + @Term + '%'
       OR tp.FullNameNormalized LIKE '%' + @Term + '%'
       OR l.Name LIKE '%' + @Term + '%'
       OR sb.Name LIKE '%' + @Term + '%')
ORDER BY ay.StartDate DESC, l.SortOrder, cg.Name;";

        var rows = await connection.QueryAsync<ClassGroupListRow>(
            new CommandDefinition(sql,
                new { YearId = academicYearId, Term = string.IsNullOrWhiteSpace(normalizedTerm) ? null : normalizedTerm },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new ClassGroupListItem(
            row.Id, row.AcademicYearId, row.AcademicYearName,
            row.LevelId, row.LevelName,
            row.SubjectId, row.SubjectName,
            row.TeacherId, row.TeacherFirstName, row.TeacherLastName, row.TeacherFatherName,
            row.RoomId, row.RoomName,
            row.Name, row.Capacity, row.StreamsText, row.IsActive, row.EnrolledCount));
    }

    public async Task<IReadOnlyList<int>> GetStreamIdsAsync(int classGroupId, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        var ids = await connection.QueryAsync<int>(
            new CommandDefinition("SELECT StreamId FROM ClassGroupStreams WHERE ClassGroupId = @ClassGroupId ORDER BY StreamId;",
                new { ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return ids.ToList();
    }

    public async Task ReplaceStreamsAsync(int classGroupId, int levelId, IReadOnlyList<int> streamIds, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        const string deleteSql = "DELETE FROM ClassGroupStreams WHERE ClassGroupId = @ClassGroupId;";
        await connection.ExecuteAsync(
            new CommandDefinition(deleteSql, new { ClassGroupId = classGroupId },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        if (streamIds.Count == 0)
            return; // قائمة فارغة = يقبل كل شعب المستوى (D-48)

        const string insertSql = @"
INSERT INTO ClassGroupStreams (ClassGroupId, LevelId, StreamId)
VALUES (@ClassGroupId, @LevelId, @StreamId);";

        foreach (var streamId in streamIds.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(insertSql, new { ClassGroupId = classGroupId, LevelId = levelId, StreamId = streamId },
                    transaction: _session.CurrentTransaction,
                    cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> HasOperationalDataAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        // D-55 (مفعَّل منذ 2.4): تسجيلات نشطة تمنع تعطيل الفوج — المنسحبة تاريخ فلا تمنع
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM ClassGroupEnrollments WHERE ClassGroupId = @Id AND Status = 1;",
                new { Id = id },
                transaction: _session.CurrentTransaction,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    private static Domain.ClassGroups.ClassGroup MapToDomain(ClassGroupRow row) =>
        Domain.ClassGroups.ClassGroup.Load(
            id: row.Id,
            academicYearId: row.AcademicYearId,
            levelId: row.LevelId,
            subjectId: row.SubjectId,
            teacherId: row.TeacherId,
            roomId: row.RoomId,
            name: row.Name,
            capacity: row.Capacity,
            isActive: row.IsActive,
            createdAtUtc: row.CreatedAtUtc,
            createdByUserId: row.CreatedByUserId,
            updatedAtUtc: row.UpdatedAtUtc,
            updatedByUserId: row.UpdatedByUserId);
}