using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.People;

/// <summary>
/// تحذير تكرار الأشخاص (6.6 — ز-2): قراءة خالصة غير مانعة — تطابق تام على الاسم الثلاثي المطبَّع (D-32/D-41)
/// بترتيب تركيب الكيان حرفاً (الأول/اللقب/الأب — لا ترتيب المعاملات!) · فشل الفحص لا يمنع الإنشاء (الواجهة تتخطّاه).
/// </summary>
public sealed class FindPersonDuplicateHandler
{
    private readonly IPersonRepository _persons;
    private readonly ILogger<FindPersonDuplicateHandler> _logger;

    public FindPersonDuplicateHandler(IPersonRepository persons, ILogger<FindPersonDuplicateHandler> logger)
    {
        _persons = persons;
        _logger = logger;
    }

    public async Task<OperationResult<PersonDuplicateMatch?>> ExecuteAsync(FindPersonDuplicateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // تركيب مطابق لـPerson.BuildNormalizedName حرفاً: الأول ثم اللقب ثم الأب — وإلا خان التطابق الصامت
            var normalized = ArabicTextNormalizer.Normalize($"{request.FirstName} {request.LastName} {request.FatherName}");
            if (normalized.Length == 0)
                return OperationResult<PersonDuplicateMatch?>.Success(null);

            var match = await _persons.GetByNormalizedFullNameAsync(normalized, cancellationToken);
            return OperationResult<PersonDuplicateMatch?>.Success(
                match is null ? null : new PersonDuplicateMatch(match.Id, match.FullName));
        }
        catch (OperationCanceledException) { throw; }   // D-64
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check duplicate person {FirstName} {LastName}", request.FirstName, request.LastName);
            return OperationResult<PersonDuplicateMatch?>.Failure("حدث خطأ غير متوقع أثناء فحص تكرار الاسم.", ErrorType.Unexpected);
        }
    }
}
