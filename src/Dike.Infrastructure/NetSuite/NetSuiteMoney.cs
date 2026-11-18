using Dike.Core;

namespace Dike.Infrastructure.NetSuite;

internal static class NetSuiteMoney
{
    internal const decimal MaximumAmount = SarMoney.MaximumAmount;

    internal static bool TryParse(string raw, out decimal value)
        => SarMoney.TryParseExact(raw, out value);

    internal static bool IsValid(decimal value) => SarMoney.IsValid(value);
}
