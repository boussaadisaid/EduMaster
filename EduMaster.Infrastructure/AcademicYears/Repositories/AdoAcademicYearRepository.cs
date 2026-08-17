using EduMaster.Application.AcademicYears.Repositories;
using EduMaster.Domain.AcademicYears;
using EduMaster.Domain.AcademicYears.ValueObjects;
using EduMaster.Infrastructure.Persistence;
using System.Data;


namespace EduMaster.Infrastructure.AcademicYears.Repositories
{
    public sealed class AdoAcademicYearRepository : IAcademicYearRepository
    {
        private readonly IAdoDbSession _session;

        public AdoAcademicYearRepository(IAdoDbSession session)
        {
            _session = session;
        }

        public async Task AddAsync(AcademicYear academicYear, CancellationToken cancellationToken = default)
        {
            //if (_session.Connection.State != System.Data.ConnectionState.Open)
            //    await _session.Connection.OpenAsync(cancellationToken);

            //await using var command = _session.Connection.CreateCommand();

            //command.CommandText = """
            //    INSERT INTO AcademicYears (Name, StartDate, EndDate, IsCurrent)
            //    VALUES (@Name, @StartDate, @EndDate, @IsCurrent);
            //    SELECT CAST(SCOPE_IDENTITY() AS INT);
            //    """;

            //command.Transaction = _session.CurrentTransaction;

            //var nameParameter = command.CreateParameter();
            //nameParameter.ParameterName = "@Name";
            //nameParameter.Value = academicYear.Name.Value;
            //command.Parameters.Add(nameParameter);

            //var startDateParameter = command.CreateParameter();
            //startDateParameter.ParameterName = "@StartDate";
            //startDateParameter.Value = academicYear.StartDate.ToDateTime(TimeOnly.MinValue);
            //command.Parameters.Add(startDateParameter);

            //var endDateParameter = command.CreateParameter();
            //endDateParameter.ParameterName = "@EndDate";
            //endDateParameter.Value = academicYear.EndDate.ToDateTime(TimeOnly.MinValue);
            //command.Parameters.Add(endDateParameter);

            //var isCurrentParameter = command.CreateParameter();
            //isCurrentParameter.ParameterName = "@IsCurrent";
            //isCurrentParameter.Value = academicYear.IsCurrent;
            //command.Parameters.Add(isCurrentParameter);

            //var result = await command.ExecuteScalarAsync(cancellationToken);

            //if (result is null)
            //    throw new DataException("لم ترجع قاعدة البيانات المعرف.");

            //var idInserted = Convert.ToInt32(result);

            //academicYear.SetId(idInserted);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ExistsByNameAsync(YearName name, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //if (_session.Connection.State != System.Data.ConnectionState.Open)
            //    await _session.Connection.OpenAsync(cancellationToken);

            //await using var command = _session.Connection.CreateCommand();

            //command.CommandText = """
            //    SELECT TOP 1 1
            //    FROM AcademicYears
            //    WHERE Name = @Name
            //    """;

            //command.Transaction = _session.CurrentTransaction;

            //var nameParameter = command.CreateParameter();
            //nameParameter.ParameterName = "@Name";
            //nameParameter.Value = name.Value;
            //command.Parameters.Add(nameParameter);

            //var result = await command.ExecuteScalarAsync(cancellationToken);

            //return result is not null;
        }

        public async Task<AcademicYear?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //if (_session.Connection.State != System.Data.ConnectionState.Open)
            //    await _session.Connection.OpenAsync(cancellationToken);

            //await using var command = _session.Connection.CreateCommand();

            //command.CommandText = """
            //    SELECT Id, Name, StartDate, EndDate, IsCurrent
            //    FROM AcademicYears
            //    WHERE Id = @Id
            //    """;

            //command.Transaction = _session.CurrentTransaction;

            //var idParameter = command.CreateParameter();
            //idParameter.ParameterName = "@Id";
            //idParameter.Value = id;
            //command.Parameters.Add(idParameter);

            //await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            //if (!await reader.ReadAsync(cancellationToken))
            //    return null;

            //var academicYearId = reader.GetInt32(reader.GetOrdinal("Id"));
            //var nameValue = reader.GetString(reader.GetOrdinal("Name"));

            //var startDateTime = reader.GetDateTime(reader.GetOrdinal("StartDate"));
            //var endDateTime = reader.GetDateTime(reader.GetOrdinal("EndDate"));

            //var isCurrent = reader.GetBoolean(reader.GetOrdinal("IsCurrent"));

            //var name = new YearName(nameValue);
            //var startDate = DateOnly.FromDateTime(startDateTime);
            //var endDate = DateOnly.FromDateTime(endDateTime);

            //return AcademicYear.Load(
            //    academicYearId,
            //    name,
            //    startDate,
            //    endDate,
            //    isCurrent);
        }

        public async Task<AcademicYear?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            
            throw new NotImplementedException();
            //if (_session.Connection.State != System.Data.ConnectionState.Open)
            //    await _session.Connection.OpenAsync(cancellationToken);

            //await using var command = _session.Connection.CreateCommand();

            //command.CommandText = """
            //    SELECT TOP 1 Id, Name, StartDate, EndDate, IsCurrent
            //    FROM AcademicYears
            //    WHERE IsCurrent = 1
            //    """;

            //command.Transaction = _session.CurrentTransaction;

            //await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            //if (!await reader.ReadAsync(cancellationToken))
            //    return null;

            //var academicYearId = reader.GetInt32(reader.GetOrdinal("Id"));
            //var nameValue = reader.GetString(reader.GetOrdinal("Name"));
            //var startDateTime = reader.GetDateTime(reader.GetOrdinal("StartDate"));
            //var endDateTime = reader.GetDateTime(reader.GetOrdinal("EndDate"));
            //var isCurrent = reader.GetBoolean(reader.GetOrdinal("IsCurrent"));

            //var yearName = new YearName(nameValue);
            //var startDateOnly = DateOnly.FromDateTime(startDateTime);
            //var endDateOnly = DateOnly.FromDateTime(endDateTime);

            //return AcademicYear.Load(
            //    academicYearId,
            //    yearName,
            //    startDateOnly,
            //    endDateOnly,
            //    isCurrent);
        }

        public async Task UpdateAsync(AcademicYear academicYear, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //if (_session.Connection.State != ConnectionState.Open)
            //    await _session.Connection.OpenAsync(cancellationToken);

            //await using var command = _session.Connection.CreateCommand();

            //command.CommandText = """
            //    UPDATE AcademicYears
            //    SET Name = @Name,
            //        StartDate = @StartDate,
            //        EndDate = @EndDate,
            //        IsCurrent = @IsCurrent
            //    WHERE Id = @Id;
            //    """;

            //command.Transaction = _session.CurrentTransaction;

            //var idParameter = command.CreateParameter();
            //idParameter.ParameterName = "@Id";
            //idParameter.Value = academicYear.Id;
            //command.Parameters.Add(idParameter);

            //var nameParameter = command.CreateParameter();
            //nameParameter.ParameterName = "@Name";
            //nameParameter.Value = academicYear.Name.Value;
            //command.Parameters.Add(nameParameter);

            //var startDateParameter = command.CreateParameter();
            //startDateParameter.ParameterName = "@StartDate";
            //startDateParameter.Value = academicYear.StartDate.ToDateTime(TimeOnly.MinValue);
            //command.Parameters.Add(startDateParameter);

            //var endDateParameter = command.CreateParameter();
            //endDateParameter.ParameterName = "@EndDate";
            //endDateParameter.Value = academicYear.EndDate.ToDateTime(TimeOnly.MinValue);
            //command.Parameters.Add(endDateParameter);

            //var isCurrentParameter = command.CreateParameter();
            //isCurrentParameter.ParameterName = "@IsCurrent";
            //isCurrentParameter.Value = academicYear.IsCurrent;
            //command.Parameters.Add(isCurrentParameter);

            //var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

            //if (affectedRows == 0)
            //    throw new DataException("لم يتم تحديث أي سجل. قد تكون السنة الدراسية غير موجودة.");
        }



    }
}
