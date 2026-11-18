using System.Text.Json;
using Dike.Core;

internal static class DomainPolicyChecks
{
    public static void Run()
    {
        var failures = new List<string>();
        var passed = 0;
        var scope = new AccountScope("company", "entity", "primary", "account", "SAR");
        var day = new DateOnly(2026, 11, 4);
        Movement Row(string id, SourceSide side, decimal amount = 100m, string reference = "R1")
            => new(id, scope, side, amount, day, reference, "revision-1");
        var bank = Row("bank-1", SourceSide.Bank);
        var book = Row("book-1", SourceSide.Book);

        void Check(string name, Action action)
        {
            try
            {
                action();
                passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception error)
            {
                failures.Add($"{name}: {error.Message}");
                Console.WriteLine($"FAIL {name}: {error.Message}");
            }
        }

        Check("Same source IDs in different accounts do not compete", () =>
        {
            var other = scope with { Account = "account-2" };
            var proposals = MatchingEngine.Propose([bank, book, bank with { Scope = other }, book with { Scope = other }]);
            Require(proposals.Count == 2 && proposals.All(x => x.Kind == ProposalKind.Proposed));
        });
        Check("Same source IDs in different companies do not compete", () =>
        {
            var other = scope with { Company = "company-2" };
            Require(MatchingEngine.Propose([bank, book, bank with { Scope = other }, book with { Scope = other }])
                .All(x => x.Kind == ProposalKind.Proposed));
        });
        Check("Proposals retain account scope for repeated source IDs", () =>
        {
            var other = scope with { Account = "account-2" };
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(
                MatchingEngine.Propose([bank, book, bank with { Scope = other }, book with { Scope = other }])));
            Require(document.RootElement.EnumerateArray().Select(x => x.GetProperty("Scope").GetProperty("Account").GetString())
                .OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "account", "account-2" }));
        });
        Check("Proposal and candidate order is independent of input order", () =>
        {
            Movement[] rows = [bank with { Id = "bank-z" }, book with { Id = "book-z" },
                bank with { Id = "bank-a" }, book with { Id = "book-a" }];
            var proposals = MatchingEngine.Propose(rows);
            Require(JsonSerializer.Serialize(proposals) == JsonSerializer.Serialize(MatchingEngine.Propose(rows.Reverse())));
            Require(proposals.Select(x => x.BankId).SequenceEqual(new[] { "bank-a", "bank-z" }));
            Require(proposals.All(x => x.BookIds.SequenceEqual(new[] { "book-a", "book-z" })));
        });
        Check("Duplicate source revisions cannot become matching candidates", () =>
            Reject(() => MatchingEngine.Propose([bank, book, book with { Version = "revision-2" }])));
        Check("Equal IDs on different source sides remain distinct", () =>
            Require(MatchingEngine.Propose([bank, book with { Id = bank.Id }]).Single().Kind == ProposalKind.Proposed));
        Check("Zero-value evidence is not an eligible matching proposal", () =>
            Require(MatchingEngine.Propose([bank with { Amount = 0 }, book with { Amount = 0 }]).Single().Kind == ProposalKind.Unmatched));
        Check("Pending ledger rows are excluded from proposals", () =>
            Require(MatchingEngine.Propose([bank, book with { Booked = false }]).Single().Kind == ProposalKind.Unmatched));
        Check("Unsupported currencies cannot reach proposal classification", () =>
            Reject(() => MatchingEngine.Propose([bank with { Scope = scope with { Currency = "USD" } },
                book with { Scope = scope with { Currency = "USD" } }])));
        Check("Excess monetary precision cannot reach matching", () =>
            Reject(() => MatchingEngine.Propose([bank with { Amount = 1.001m }, book with { Amount = 1.001m }])));
        Check("Unknown source side cannot be silently ignored by matching", () =>
            Reject(() => MatchingEngine.Propose([bank, book, Row("unknown", (SourceSide)42)])));
        Check("Matching rejects missing revision evidence", () =>
            Reject(() => MatchingEngine.Propose([bank, book with { Version = " " }])));
        Check("Blank references require human review", () =>
            Require(MatchingEngine.Propose([bank with { Reference = " " }, book with { Reference = "" }]).Single().Kind == ProposalKind.RequiresReview));
        Check("Reference normalization remains trim and ordinal ignore-case", () =>
            Require(MatchingEngine.Propose([bank with { Reference = " abc " }, book with { Reference = "ABC" }]).Single().Kind == ProposalKind.Proposed));
        Check("Punctuation is not removed from references", () =>
            Require(MatchingEngine.Propose([bank with { Reference = "A-B" }, book with { Reference = "AB" }]).Single().Kind == ProposalKind.RequiresReview));
        Check("Exactly three business days are eligible and four are not", () =>
        {
            var start = new DateOnly(2026, 11, 5);
            Require(MatchingEngine.Propose([bank with { Date = start }, book with { Date = new(2026, 11, 10) }]).Single().Kind == ProposalKind.Proposed);
            Require(MatchingEngine.Propose([bank with { Date = start }, book with { Date = new(2026, 11, 11) }]).Single().Kind == ProposalKind.Unmatched);
        });
        Check("Business-day counting handles the maximum date", () =>
        {
            Require(MatchingEngine.BusinessDaysBetween(DateOnly.MaxValue, DateOnly.MaxValue) == 0);
            Require(MatchingEngine.BusinessDaysBetween(DateOnly.MaxValue.AddDays(-6), DateOnly.MaxValue) == SlowBusinessDays(DateOnly.MaxValue.AddDays(-6), DateOnly.MaxValue));
        });
        Check("Business-day counting handles the full DateOnly range", () =>
            Require(MatchingEngine.BusinessDaysBetween(DateOnly.MinValue, DateOnly.MaxValue) == 2608613));
        Check("Business calendar agrees with enumerated days in both directions", () =>
        {
            for (var offset = 0; offset < 7; offset++)
                for (var length = 0; length <= 35; length++)
                {
                    var start = new DateOnly(2026, 11, 1).AddDays(offset);
                    var end = start.AddDays(length);
                    var expected = SlowBusinessDays(start, end);
                    Require(MatchingEngine.BusinessDaysBetween(start, end) == expected);
                    Require(MatchingEngine.BusinessDaysBetween(end, start) == expected);
                }
        });

        Check("Negative one-to-many review groups balance exactly", () =>
            ReviewPolicy.ValidateSubmission([bank with { Amount = -100 }, book with { Amount = -60 }, book with { Id = "book-2", Amount = -40 }], "Evidence checked"));
        Check("Many-to-one review groups balance exactly", () =>
            ReviewPolicy.ValidateSubmission([bank with { Amount = 60 }, bank with { Id = "bank-2", Amount = 40 }, book], "Evidence checked"));
        Check("Many-to-many review remains rejected", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = 60 }, bank with { Id = "bank-2", Amount = 40 },
                book with { Amount = 70 }, book with { Id = "book-2", Amount = 30 }], "review")));
        Check("Review requires a reason", () => Reject(() => ReviewPolicy.ValidateSubmission([bank, book], " ")));
        Check("Review rejects zero-value rows", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = 0 }, book with { Amount = 0 }], "review")));
        Check("Review rejects pending rows", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank, book with { Booked = false }], "review")));
        Check("Review rejects foreign account scope", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank, book with { Scope = scope with { Account = "other" } }], "review")));
        Check("Review rejects an unknown side even when bank and book totals agree", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank, book, Row("unknown", (SourceSide)42, 50)], "review")));
        Check("Review rejects unsupported currency even when totals agree", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Scope = scope with { Currency = "USD" } },
                book with { Scope = scope with { Currency = "USD" } }], "review")));
        Check("Review rejects missing source identity", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Id = " " }, book], "review")));
        Check("Review rejects missing source revision", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank, book with { Version = "" }], "review")));
        foreach (var incomplete in new[] { scope with { Company = "" }, scope with { Subsidiary = " " },
            scope with { AccountingBook = "" }, scope with { Account = " " }, scope with { Currency = "" } })
            Check($"Review rejects incomplete scope {JsonSerializer.Serialize(incomplete)}", () =>
                Reject(() => ReviewPolicy.ValidateSubmission([bank with { Scope = incomplete }, book with { Scope = incomplete }], "review")));
        Check("Review rejects excess monetary precision without rounding", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = 1.001m }, book with { Amount = 1.001m }], "review")));
        Check("Review rejects positive decimal(19,2) overflow", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = 100000000000000000m }, book with { Amount = 100000000000000000m }], "review")));
        Check("Review rejects negative decimal(19,2) overflow", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = -100000000000000000m }, book with { Amount = -100000000000000000m }], "review")));
        Check("Review accepts exact monetary limits and trailing zeros", () =>
        {
            foreach (var amount in new[] { 99999999999999999.99m, -99999999999999999.99m, 125.5000m })
                ReviewPolicy.ValidateSubmission([bank with { Amount = amount }, book with { Amount = amount }], "review");
        });
        Check("Netting a SAR 50 opposite-sign fee does not bypass direction policy", () =>
            Reject(() => ReviewPolicy.ValidateSubmission([bank with { Amount = 5000 }, book with { Amount = 5050 },
                book with { Id = "fee", Amount = -50 }], "Fee investigation")));
        Check("Approval requires a known preparer", () => Reject(() => ReviewPolicy.ValidateApproval(" ", "controller", ReviewStatus.Submitted)));
        Check("Approval requires a known approver", () => Reject(() => ReviewPolicy.ValidateApproval("preparer", " ", ReviewStatus.Submitted)));
        Check("Case variation cannot bypass separation of duties", () => Reject(() => ReviewPolicy.ValidateApproval("Alice", "alice", ReviewStatus.Submitted)));
        Check("Distinct controller may approve a submitted group", () => ReviewPolicy.ValidateApproval("preparer", "controller", ReviewStatus.Submitted));
        Check("Every non-submitted state rejects approval", () =>
        {
            foreach (var status in Enum.GetValues<ReviewStatus>().Where(x => x != ReviewStatus.Submitted))
                Reject(() => ReviewPolicy.ValidateApproval("preparer", "controller", status));
        });

        Console.WriteLine($"Domain policy checks: {passed} passed, {failures.Count} failed.");
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected domain outcome was not observed.");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected rejection was not observed.");
    }

    private static int SlowBusinessDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var number = start.DayNumber + 1; number <= end.DayNumber; number++)
            if (DateOnly.FromDayNumber(number).DayOfWeek is not DayOfWeek.Friday and not DayOfWeek.Saturday) count++;
        return count;
    }
}
