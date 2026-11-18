using Dike.Core;

if (args.Contains("--hosted-storage"))
{
    await HostedStorageChecks.RunAsync();
    return;
}

var scope = new AccountScope("company", "entity", "primary", "bank-1", "SAR");
var day = new DateOnly(2026, 11, 4);
Movement Row(string id, SourceSide side, decimal amount, string reference = "R1", bool booked = true)
    => new(id, scope, side, amount, day, reference, "v1", booked);
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine($"PASS {name}"); }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { Console.WriteLine($"PASS {name}"); return; } throw new Exception(name); }
var bank = Row("b1", SourceSide.Bank, 5000);
var book = Row("g1", SourceSide.Book, 5000);
Check(MatchingEngine.Propose([bank, book]).Single().Kind == ProposalKind.Proposed, "Exact match");
Check(MatchingEngine.Propose([bank, book with { Amount = 5050 }]).Single().Kind == ProposalKind.AmountMismatch, "SAR 50 remains unexplained");
Check(MatchingEngine.Propose([bank, bank with { Id = "b2" }, book]).All(x => x.Kind == ProposalKind.Ambiguous), "Competing proposals");
Check(MatchingEngine.Propose([bank with { Booked = false }, book]).Count == 0, "Pending exclusion");
Check(MatchingEngine.Propose([bank, book with { Scope = scope with { Account = "other" } }]).Single().Kind == ProposalKind.Unmatched, "Account isolation");
Check(MatchingEngine.BusinessDaysBetween(new(2026, 11, 5), new(2026, 11, 8)) == 1, "Friday Saturday weekend");
ReviewPolicy.ValidateSubmission([bank, book with { Amount = 3000 }, book with { Id = "g2", Amount = 2000 }], "Evidence checked");
Console.WriteLine("PASS Balanced one-to-many");
Reject(() => ReviewPolicy.ValidateSubmission([bank, book with { Amount = 5050 }], "review"), "Reject imbalance");
Reject(() => ReviewPolicy.ValidateSubmission([bank, book, book], "review"), "Reject duplicate rows");
Reject(() => ReviewPolicy.ValidateApproval("alice", "alice", ReviewStatus.Submitted), "Separation of duties");
Reject(() => ReviewPolicy.ValidateApproval("alice", "bob", ReviewStatus.Draft), "Reject approval of draft");
Console.WriteLine("All domain regression checks passed.");
DomainPolicyChecks.Run();
await NetSuiteChecks.RunAsync();
GlValidationChecks.Run();
await StaffServiceChecks.RunAsync();
await OdooChecks.RunAsync();
await OdooWebChecks.RunAsync();
