namespace Dike.Infrastructure.Odoo;

public sealed record OdooAccess(int UserId, IReadOnlyList<int> PermittedCompanyIds);
public sealed record OdooCompany(int Id, string Name, int CurrencyId, string CurrencyLabel);
public sealed record OdooAccount(int Id, string Code, string Name, string AccountType, int? CurrencyId);
public sealed record OdooCatalog<T>(int AuthenticatedUserId, IReadOnlyList<T> Rows, bool HasMore, int? NextAfterId);
public sealed record OdooGlLine(int Id, int MoveId, int JournalId, DateOnly Date, decimal Debit, decimal Credit, decimal Balance);
public sealed record OdooGlSample(int AuthenticatedUserId, int CompanyId, int AccountId,
    DateOnly From, DateOnly To, int Limit, IReadOnlyList<OdooGlLine> Rows, bool HasMore)
{
    public string Currency => "SAR";
    public bool SampleOnly => true;
    public string AcceptanceStatus => "UnverifiedSourceContractAndReportAgreement";
}
