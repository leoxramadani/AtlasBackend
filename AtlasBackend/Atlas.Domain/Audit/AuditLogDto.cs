namespace Atlas.Application.DTOs
{
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime RetainUntilUtc { get; set; }
    }
}
