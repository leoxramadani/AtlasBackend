using System.Text.RegularExpressions;
using Atlas.Domain.Exceptions;

namespace Atlas.Domain.Model.Markets;

public class MarketMaRule : IMarketRule
{
    public MarketCode Market => MarketCode.MA;
    public bool RequiresInPersonActivation => false;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || !Regex.IsMatch(nationalId, @"^\d{13}$"))
        {
            throw new MarketValidationException("Market MA requires a 13-digit Personal Number.");
        }
    }
}

public class MarketMbRule : IMarketRule
{
    public MarketCode Market => MarketCode.MB;
    public bool RequiresInPersonActivation => false;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || !Regex.IsMatch(nationalId, @"^\d{13}$"))
        {
            throw new MarketValidationException("Market MB requires a 13-digit Personal Number.");
        }
    }
}

public class MarketMcRule : IMarketRule
{
    public MarketCode Market => MarketCode.MC;
    public bool RequiresInPersonActivation => false;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        // 9 characters, alphanumeric, letter in position 9
        if (string.IsNullOrWhiteSpace(nationalId) || !Regex.IsMatch(nationalId, @"^[a-zA-Z0-9]{8}[a-zA-Z]$"))
        {
            throw new MarketValidationException("Market MC requires a 9-character Unified Citizen ID ending with a letter.");
        }
    }
}

public class MarketMdRule : IMarketRule
{
    public MarketCode Market => MarketCode.MD;
    // Annex B: Applicant must attend a branch in person. Wet signature required before the account may be activated.
    public bool RequiresInPersonActivation => true;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || !Regex.IsMatch(nationalId, @"^\d{10}$"))
        {
            throw new MarketValidationException("Market MD requires a 10-digit Citizen Number.");
        }
    }
}

public class MarketMeRule : IMarketRule
{
    public MarketCode Market => MarketCode.ME;
    public bool RequiresInPersonActivation => false;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        // 10 digits with a check digit algorithm
        if (string.IsNullOrWhiteSpace(nationalId) || !Regex.IsMatch(nationalId, @"^\d{10}$"))
        {
            throw new MarketValidationException("Market ME requires a 10-digit Civil Number.");
        }

        // Modulo 11 check digit verification
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            sum += (nationalId[i] - '0') * (10 - i);
        }
        int remainder = sum % 11;
        int expectedCheckDigit = remainder < 2 ? 0 : 11 - remainder;
        int actualCheckDigit = nationalId[9] - '0';

        if (actualCheckDigit != expectedCheckDigit && expectedCheckDigit != 10)
        {
            throw new MarketValidationException($"Market ME Civil Number failed check-digit checksum (expected {expectedCheckDigit}).");
        }
    }
}

public class MarketMfRule : IMarketRule
{
    public MarketCode Market => MarketCode.MF;
    public bool RequiresInPersonActivation => false;

    public void ValidateIdentifier(string? nationalId, string? passportNumber)
    {
        // Personal Number not issued to non-citizen residents. Passport number accepted in lieu.
        if (string.IsNullOrWhiteSpace(nationalId) && string.IsNullOrWhiteSpace(passportNumber))
        {
            throw new MarketValidationException("Market MF requires either a Personal Number or a Passport Number in lieu for non-citizen residents.");
        }
    }
}

public static class MarketRuleRegistry
{
    private static readonly Dictionary<MarketCode, IMarketRule> Rules = new()
    {
        { MarketCode.MA, new MarketMaRule() },
        { MarketCode.MB, new MarketMbRule() },
        { MarketCode.MC, new MarketMcRule() },
        { MarketCode.MD, new MarketMdRule() },
        { MarketCode.ME, new MarketMeRule() },
        { MarketCode.MF, new MarketMfRule() }
    };

    public static IMarketRule GetRule(MarketCode market)
    {
        if (Rules.TryGetValue(market, out var rule))
        {
            return rule;
        }

        throw new MarketValidationException($"Market {market} is not supported.");
    }
}
