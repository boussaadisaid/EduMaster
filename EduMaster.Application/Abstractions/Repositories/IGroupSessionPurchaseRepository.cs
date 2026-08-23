using EduMaster.Domain.Scheduling;

namespace EduMaster.Application.Abstractions.Repositories;

/// <summary>append-only (D-91) — الكتابة فقط هنا؛ مجموعات الرصيد تُحسب في الاستعلامات المسطّحة مباشرة (لا قراءة كيانية له بعد)</summary>
public interface IGroupSessionPurchaseRepository
{
    Task AddAsync(GroupSessionPurchase purchase, CancellationToken cancellationToken = default);
}