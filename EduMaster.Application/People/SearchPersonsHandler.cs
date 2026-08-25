using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EduMaster.Application.People;

public sealed class SearchPersonsHandler
{
    private readonly IPersonRepository _persons;
    private readonly ILogger<SearchPersonsHandler> _logger;

    public SearchPersonsHandler(IPersonRepository persons, ILogger<SearchPersonsHandler> logger)
    {
        _persons = persons;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<PersonListItem>>> ExecuteAsync(
        string? searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            // ح-3: المصطلح يُطبَّع بنفس دالة الكتابة تماماً — وإلا انفصلت القراءة عن الكتابة
            var normalized = string.IsNullOrWhiteSpace(searchTerm)
                ? null
                : ArabicTextNormalizer.Normalize(searchTerm);

            var people = await _persons.SearchAsync(normalized, cancellationToken);

            var items = people.Select(p => new PersonListItem(
                p.Id,
                p.FirstName.Value,
                p.LastName.Value,
                p.FatherName?.Value,
                p.BirthDate?.Value,
                p.Gender,
                p.Phone?.Value,
                p.Phone2?.Value,
                p.Email?.Value,
                p.Address,
                p.IsActive)).ToList();

            return OperationResult<IReadOnlyList<PersonListItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;   // D-64: إلغاء طلب سابق أثناء الكتابة — ليس خطأً، يعالجه المتصل بصمت
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // D-64: SqlClient قد يلفّ إلغاء الأمر الجاري داخل SqlException («Operation cancelled by user») — الإلغاء ليس خطأً
            throw new OperationCanceledException("People search cancelled.", ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search people with term {SearchTerm}", searchTerm);
            return OperationResult<IReadOnlyList<PersonListItem>>.Failure(
                "حدث خطأ غير متوقع أثناء البحث عن الأشخاص.", ErrorType.Unexpected);
        }
    }
}