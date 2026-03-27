namespace AgroAdmin.Shared.Dto.Health
{
    public class ComponentHealth
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string? Message { get; set; }
        public long? ResponseTimeMs { get; set; }
    }
}
