using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Academic;
using Microsoft.Extensions.Logging;


namespace EduMaster.Application.Academic
{
    public sealed class GetSubjectsHandler
    {
        private readonly ISubjectRepository _subjects;
        private readonly ILogger<GetSubjectsHandler> _logger;

        public GetSubjectsHandler(ISubjectRepository subjects, ILogger<GetSubjectsHandler> logger)
        {
            _subjects = subjects;
            _logger = logger;
        }

        public async Task<OperationResult<IReadOnlyList<Subject>>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var items = await _subjects.GetAllAsync(cancellationToken);
                return OperationResult<IReadOnlyList<Subject>>.Success(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load subjects");
                return OperationResult<IReadOnlyList<Subject>>.Failure("حدث خطأ غير متوقع أثناء تحميل المواد.", ErrorType.Unexpected);
            }
        }
    }
}
