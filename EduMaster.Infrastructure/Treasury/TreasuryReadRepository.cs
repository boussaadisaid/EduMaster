
using Dapper;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Treasury;
using EduMaster.Domain.Treasury;
using EduMaster.Infrastructure.Persistence;

namespace EduMaster.Infrastructure.Treasury;

public sealed class TreasuryReadRepository : ITreasuryReadRepository
{
    private readonly IAdoDbSession _session;
    public TreasuryReadRepository(IAdoDbSession session) => _session = session;
    private sealed record Row(int SourceKind, int SourceId, int TreasuryAccountId, string AccountName, DateTime MovementDate, string Description, long IncomingCentimes, long OutgoingCentimes, long DeltaCentimes, byte? TransactionKind, string? Note);
    private const string Cte = @"
WITH Movements AS
(
 SELECT 1 SourceKind,p.Id SourceId,p.TreasuryAccountId,ta.Name AccountName,p.PaidOn MovementDate,
        CASE WHEN p.Kind=1 THEN N'قبض: ' ELSE N'استرجاع: ' END+CONCAT_WS(N' ',sp.FirstName,sp.LastName,sp.FatherName) Description,
        CASE WHEN p.Kind=1 THEN p.AmountCentimes ELSE 0 END IncomingCentimes,
        CASE WHEN p.Kind=2 THEN p.AmountCentimes ELSE 0 END OutgoingCentimes,
        CASE WHEN p.Kind=1 THEN p.AmountCentimes ELSE -p.AmountCentimes END DeltaCentimes,
        CAST(NULL AS TINYINT) TransactionKind,p.Note Note
 FROM dbo.Payments p INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=p.TreasuryAccountId INNER JOIN dbo.Students s ON s.Id=p.StudentId INNER JOIN dbo.Persons sp ON sp.Id=s.PersonId
 UNION ALL
 SELECT 2,po.Id,po.TreasuryAccountId,ta.Name,po.PayoutDate,
        CASE WHEN po.AmountCentimes<0 THEN N'تصحيح صرف أجر: ' ELSE N'صرف أجر: ' END+
        CASE WHEN po.PayeeKind=1 THEN CONCAT_WS(N' ',tp.FirstName,tp.LastName,tp.FatherName) ELSE CONCAT_WS(N' ',ep.FirstName,ep.LastName,ep.FatherName) END,
        CASE WHEN po.AmountCentimes<0 THEN -po.AmountCentimes ELSE 0 END,
        CASE WHEN po.AmountCentimes>0 THEN po.AmountCentimes ELSE 0 END,
        -po.AmountCentimes,CAST(NULL AS TINYINT),po.Note
 FROM dbo.Payouts po INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=po.TreasuryAccountId
 LEFT JOIN dbo.Teachers t ON t.Id=po.TeacherId LEFT JOIN dbo.Persons tp ON tp.Id=t.PersonId
 LEFT JOIN dbo.Employees e ON e.Id=po.EmployeeId LEFT JOIN dbo.Persons ep ON ep.Id=e.PersonId
 UNION ALL
 SELECT 3,e.Id,e.TreasuryAccountId,ta.Name,e.ExpenseDate,c.Name,0,e.AmountCentimes,-e.AmountCentimes,CAST(NULL AS TINYINT),e.Note
 FROM dbo.Expenses e INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=e.TreasuryAccountId INNER JOIN dbo.ExpenseCategories c ON c.Id=e.ExpenseCategoryId WHERE e.IsDeleted=0
 UNION ALL
 SELECT 4,tt.Id,tt.TreasuryAccountId,ta.Name,tt.TransactionDate,
        CASE WHEN tt.Kind=1 THEN N'دخل آخر' ELSE N'مصروف آخر' END,
        CASE WHEN tt.Kind=1 THEN tt.AmountCentimes ELSE 0 END,
        CASE WHEN tt.Kind=2 THEN tt.AmountCentimes ELSE 0 END,
        CASE WHEN tt.Kind=1 THEN tt.AmountCentimes ELSE -tt.AmountCentimes END,
        tt.Kind,tt.Note
 FROM dbo.TreasuryTransactions tt INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=tt.TreasuryAccountId WHERE tt.IsDeleted=0
 UNION ALL
 SELECT 5,tr.Id,tr.FromTreasuryAccountId,fa.Name,tr.TransferDate,N'تحويل إلى: '+ta.Name,0,tr.AmountCentimes,-tr.AmountCentimes,CAST(NULL AS TINYINT),tr.Note
 FROM dbo.TreasuryTransfers tr INNER JOIN dbo.TreasuryAccounts fa ON fa.Id=tr.FromTreasuryAccountId INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=tr.ToTreasuryAccountId WHERE tr.IsDeleted=0
 UNION ALL
 SELECT 6,tr.Id,tr.ToTreasuryAccountId,ta.Name,tr.TransferDate,N'تحويل من: '+fa.Name,tr.AmountCentimes,0,tr.AmountCentimes,CAST(NULL AS TINYINT),tr.Note
 FROM dbo.TreasuryTransfers tr INNER JOIN dbo.TreasuryAccounts fa ON fa.Id=tr.FromTreasuryAccountId INNER JOIN dbo.TreasuryAccounts ta ON ta.Id=tr.ToTreasuryAccountId WHERE tr.IsDeleted=0
)
";
    public async Task<IReadOnlyList<TreasuryMovementItem>> GetMovementsAsync(int? treasuryAccountId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var rows = await c.QueryAsync<Row>(new CommandDefinition(Cte + @"
SELECT SourceKind,SourceId,TreasuryAccountId,AccountName,MovementDate,Description,IncomingCentimes,OutgoingCentimes,DeltaCentimes,TransactionKind,Note FROM Movements
WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND (@From IS NULL OR MovementDate>=@From) AND (@To IS NULL OR MovementDate<=@To)
ORDER BY MovementDate DESC,SourceId DESC;", new { AccountId = treasuryAccountId, From = from?.ToDateTime(TimeOnly.MinValue), To = to?.ToDateTime(TimeOnly.MinValue) }, transaction: _session.CurrentTransaction, cancellationToken: cancellationToken));
        return rows.Select(r => new TreasuryMovementItem(r.SourceKind, r.SourceId, r.TreasuryAccountId, r.AccountName, DateOnly.FromDateTime(r.MovementDate), r.Description, r.IncomingCentimes, r.OutgoingCentimes, r.DeltaCentimes, r.TransactionKind is null ? null : (TreasuryTransactionKind)r.TransactionKind, r.Note, r.SourceKind is 4 or 5 or 6, r.SourceKind is 5 or 6)).ToList();
    }
    public async Task<TreasurySummaryItem> GetSummaryAsync(int? treasuryAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var c = await _session.GetOpenConnectionAsync(cancellationToken);
        var row = await c.QuerySingleAsync<dynamic>(Cte + @"
SELECT
 ISNULL((SELECT SUM(OpeningBalanceCentimes) FROM dbo.TreasuryAccounts WHERE @AccountId IS NULL OR Id=@AccountId),0)+ISNULL((SELECT SUM(DeltaCentimes) FROM Movements WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND MovementDate<@From),0) OpeningBalance,
 ISNULL((SELECT SUM(IncomingCentimes) FROM Movements WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND MovementDate BETWEEN @From AND @To AND (@AccountId IS NOT NULL OR SourceKind NOT IN (5,6))),0) PeriodIncoming,
 ISNULL((SELECT SUM(OutgoingCentimes) FROM Movements WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND MovementDate BETWEEN @From AND @To AND (@AccountId IS NOT NULL OR SourceKind NOT IN (5,6))),0) PeriodOutgoing,
 ISNULL((SELECT SUM(DeltaCentimes) FROM Movements WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND MovementDate BETWEEN @From AND @To),0) PeriodNet,
 ISNULL((SELECT SUM(OpeningBalanceCentimes) FROM dbo.TreasuryAccounts WHERE @AccountId IS NULL OR Id=@AccountId),0)+ISNULL((SELECT SUM(DeltaCentimes) FROM Movements WHERE (@AccountId IS NULL OR TreasuryAccountId=@AccountId) AND MovementDate<=@To),0) ClosingBalance;", new { AccountId = treasuryAccountId, From = from.ToDateTime(TimeOnly.MinValue), To = to.ToDateTime(TimeOnly.MinValue) }, transaction: _session.CurrentTransaction);
        return new TreasurySummaryItem((long)row.OpeningBalance, (long)row.PeriodIncoming, (long)row.PeriodOutgoing, (long)row.PeriodNet, (long)row.ClosingBalance);
    }
}
