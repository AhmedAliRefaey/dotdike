using System.Security.Cryptography;
using System.Text;

namespace Dike.Infrastructure.NetSuite;

// Identifies the existing query for T02 review. Candidate versions are not finance approval.
public static class NetSuiteQueryContract
{
    public const string QueryVersion = "netsuite-posted-gl-candidate-v1";
    public const string IdentityVersion = "transaction-line-book-account-candidate-v1";

    public const string ScopeSql = "SELECT a.id, a.accttype, c.symbol AS accountcurrency, sc.symbol AS basecurrency, b.isprimary FROM account a JOIN currency c ON c.id = a.currency, subsidiary s JOIN currency sc ON sc.id = s.currency, accountingbook b WHERE a.id = ? AND s.id = ? AND b.id = ? AND a.isinactive = 'F'";

    public const string PostedGlSql = "SELECT t.id AS transactionid, al.transactionline AS lineid, al.accountingbook AS bookid, al.account AS accountid, l.subsidiary AS subsidiaryid, TO_CHAR(t.trandate, 'YYYY-MM-DD') AS postingdate, t.tranid AS reference, NVL(al.debit, 0) AS debit, NVL(al.credit, 0) AS credit FROM transaction t JOIN transactionline l ON l.transaction = t.id JOIN transactionaccountingline al ON al.transaction = l.transaction AND al.transactionline = l.id WHERE t.posting = 'T' AND al.posting = 'T' AND al.account = ? AND l.subsidiary = ? AND al.accountingbook = ? AND t.trandate >= TO_DATE(?, 'YYYY-MM-DD') AND t.trandate < TO_DATE(?, 'YYYY-MM-DD') + 1 ORDER BY t.id, al.transactionline, al.accountingbook, al.account";

    // Hash both statements, with exactly one LF between them, independently of OS line endings.
    public static string QuerySha256 { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(ScopeSql + "\n" + PostedGlSql)));
}
