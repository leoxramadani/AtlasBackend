namespace Atlas.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class InvalidApplicationStateException : DomainException
{
    public InvalidApplicationStateException(string message) : base(message) { }
}

public class MarketValidationException : DomainException
{
    public MarketValidationException(string message) : base(message) { }
}
