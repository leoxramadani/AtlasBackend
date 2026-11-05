namespace Atlas.Application.Contracts;

public record ApplicationSubmittedEvent(
    Guid ApplicationId,
    string Market,
    string? PassportImageBase64,
    string? SelfieImageBase64,
    string? IdNowOverride,
    string? WorldCheckOverride,
    string AttributedActor
);

public record ApplicationApprovedEvent(
    Guid ApplicationId,
    string Market,
    string AttributedActor
);

