namespace Atlas.Domain.Model.Applicants;

public enum DocumentType
{
    Passport,
    IdCard,
    Selfie
}

public class Document
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DocumentType Type { get; private set; }
    public string? ContentHash { get; private set; }
    public string? StorageReference { get; private set; }

    private Document() { } // EF Core

    public Document(DocumentType type, string? contentHash, string? storageReference = null)
    {
        Type = type;
        ContentHash = contentHash;
        StorageReference = storageReference;
    }

}

public class Applicant
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public DateOnly DateOfBirth { get; private set; }
    public string? NationalId { get; private set; }
    public string? PassportNumber { get; private set; }
    public string Email { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public bool TermsAccepted { get; private set; }
    public bool IsErased { get; private set; }

    private Applicant() { } // EF Core

    public Applicant(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        string? nationalId,
        string? passportNumber,
        string email,
        string phone,
        bool termsAccepted)
    {
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        NationalId = nationalId;
        PassportNumber = passportNumber;
        Email = email;
        Phone = phone;
        TermsAccepted = termsAccepted;
        IsErased = false;
    }
}
