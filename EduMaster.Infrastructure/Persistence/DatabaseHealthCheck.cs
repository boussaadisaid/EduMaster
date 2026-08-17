using EduMaster.Application.Abstractions;
using EduMaster.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace EduMaster.Infrastructure.Persistence;

public sealed class DatabaseHealthCheck : IDatabaseHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseHealthCheck> _logger ;
    public DatabaseHealthCheck(string connectionString, ILogger<DatabaseHealthCheck> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<OperationResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");   // التفاصيل → السجل (للمطوّر)
            return OperationResult.Failure(
                "تعذّر الاتصال بقاعدة البيانات. تأكد أن SQL Server يعمل ثم أعد المحاولة.",
                ErrorType.Unexpected);                              // للمستخدم: عربية واضحة تقترح فعلاً
        }
    }
}