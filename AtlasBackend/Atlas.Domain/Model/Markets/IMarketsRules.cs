namespace Atlas.Domain.Model.Markets;

public enum MarketCode
{
    MA,
    MB,
    MC,
    MD,
    ME,
    MF
}

public interface IMarketRule
{
    MarketCode Market { get; }
    bool RequiresInPersonActivation { get; }
    void ValidateIdentifier(string? nationalId, string? passportNumber);
}
