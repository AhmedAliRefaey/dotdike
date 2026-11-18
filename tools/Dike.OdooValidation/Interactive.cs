using System.Globalization;
using System.Text.Json;
using Dike.Infrastructure.Odoo;

internal static class Interactive
{
    internal static async Task RunAsync(OdooReadOnlyClient client, Uri baseUri, CancellationToken ct)
    {
        Console.WriteLine("Odoo read-only test. Accounts reflect the API user's permissions, not separate Dike user assignments.");
        var companies = new List<OdooCompany>();
        var afterCompany = 0;
        int companyId;
        while (true)
        {
            var page = await client.ListCompaniesAsync(afterCompany, ct);
            companies.AddRange(page.Rows);
            Console.WriteLine($"Authenticated Odoo user ID: {page.AuthenticatedUserId}");
            Console.WriteLine("ID       Company                                             Currency");
            foreach (var row in page.Rows) Console.WriteLine($"{row.Id,-8} {Label(row.Name),-51} {Label(row.CurrencyLabel)}");
            if (companies.Count == 0) throw new OdooException("scope", "No companies are visible to the integration user.");
            var answer = Ask(page.HasMore ? "Select a loaded company ID, or type more" : "Select a loaded company ID");
            if (answer == "more" && page.HasMore) { afterCompany = page.NextAfterId!.Value; continue; }
            companyId = Id(answer);
            if (!companies.Any(company => company.Id == companyId)) throw SelectionError();
            break;
        }

        var accounts = new List<OdooAccount>();
        var afterAccount = 0;
        int[] accountIds;
        while (true)
        {
            var page = await client.ListAccountsAsync(companyId, afterAccount, ct, bankOnly: true);
            accounts.AddRange(page.Rows);
            Console.WriteLine("ID       Code             Bank/cash account");
            foreach (var row in page.Rows) Console.WriteLine($"{row.Id,-8} {Label(row.Code),-16} {Label(row.Name)}");
            Console.WriteLine("For this SAR test, choose accounts whose AccountType is asset_cash (bank/cash).");
            if (accounts.Count == 0) throw new OdooException("scope", "No accounts are visible in the selected company.");
            var answer = Ask(page.HasMore ? "Select 1-5 loaded account IDs separated by commas, or type more" : "Select 1-5 loaded account IDs separated by commas");
            if (answer == "more" && page.HasMore) { afterAccount = page.NextAfterId!.Value; continue; }
            accountIds = answer.Split(',').Select(part => Id(part.Trim())).ToArray();
            if (accountIds.Length is < 1 or > 5 || accountIds.Distinct().Count() != accountIds.Length ||
                accountIds.Any(id => !accounts.Any(account => account.Id == id && account.AccountType == "asset_cash")))
                throw SelectionError();
            break;
        }

        var mode = Ask("Period: week, month, or range").ToLowerInvariant();
        var start = Ask(mode == "month" ? "Month (YYYY-MM)" : mode == "week" ? "Week starting (YYYY-MM-DD)" : "From date (YYYY-MM-DD)");
        var end = mode == "range" ? Ask("To date (YYYY-MM-DD, inclusive; maximum 31 days)") : null;
        var period = OdooPeriod.Parse(mode, start, end);
        var limitText = Ask("Maximum rows per account (1-50; Enter for 10)", allowEmpty: true);
        var limit = limitText.Length == 0 ? 10 : Id(limitText);
        if (limit > OdooReadOnlyClient.MaxSampleRows) throw SelectionError();
        Console.WriteLine($"Reading posted entries from {period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} through {period.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} for {accountIds.Length} selected account(s).");
        // Buffer the small results so a later failed account cannot produce a seemingly complete batch.
        var results = new List<OdooGlSample>();
        foreach (var accountId in accountIds)
            results.Add(await client.ReadPostedSampleAsync(companyId, accountId, period.From, period.To, limit, ct));
        Print(new { Target = baseUri.GetLeftPart(UriPartial.Authority), ReadOnly = true, SampleOnly = true, Results = results });
        Console.WriteLine("This is a bounded sample. HasMore indicates truncation; zero rows do not prove zero account activity. No complete GL or finance acceptance is claimed.");
    }

    private static void Print<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private static string Label(string value) => JsonSerializer.Serialize(value.Length > 80 ? value[..80] + "..." : value);
    private static string Ask(string prompt, bool allowEmpty = false)
    {
        Console.Write(prompt + ": ");
        var value = Console.ReadLine()?.Trim();
        if (value == null) throw new OperationCanceledException();
        if (!allowEmpty && value.Length == 0) throw SelectionError();
        return value;
    }
    private static int Id(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw SelectionError();
    private static OdooException SelectionError() => new("selection", "Choose valid IDs from the loaded list and valid filter values. No GL request was made for the invalid selection.");
}
