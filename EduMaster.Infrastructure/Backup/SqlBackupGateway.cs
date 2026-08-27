using EduMaster.Application.Abstractions;
using EduMaster.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace EduMaster.Infrastructure.Backup;

/// <summary>
/// بوابة النسخ الخام (6.5 — ن-1): BACKUP DATABASE على الاتصال القائم بلا معاملة (BACKUP ممنوع داخلها) ·
/// اسم القاعدة من الاتصال نفسه (لا ثابت مكرر) · SQL 3201/5 (تعذّر فتح جهاز النسخ — خدمة SQL Server هي الكاتبة لا التطبيق) ← BackupAccessDeniedException ·
/// رفيعة قصداً وتُجرَّب يدوياً — المنطق مختبَر فوقها بالمزيّف (ن-6)
/// </summary>
public sealed class SqlBackupGateway : IBackupGateway
{
    private readonly IAdoDbSession _session;

    public SqlBackupGateway(IAdoDbSession session) => _session = session;

    public async Task BackupDatabaseAsync(string targetBakFilePath, CancellationToken cancellationToken = default)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "BACKUP DATABASE @Database TO DISK = @Path;";   // T-SQL يقبل المتغيرات هنا (اسم القاعدة والمسار)
        command.CommandTimeout = 600;   // قاعدة مدرسة صغيرة — سقف سخيّ حتى لا يُقتطع النسخ منتصفه

        var database = command.CreateParameter();
        database.ParameterName = "@Database";
        database.Value = connection.Database;   // من الاتصال نفسه — لا ثابت مكرر
        command.Parameters.Add(database);

        var path = command.CreateParameter();
        path.ParameterName = "@Path";
        path.Value = targetBakFilePath;
        command.Parameters.Add(path);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == 3201 || ex.Number == 5)
        {
            throw new BackupAccessDeniedException(targetBakFilePath, ex);
        }
    }
}
