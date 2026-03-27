namespace AgroAdmin.Shared.Dto.Health;

public class HealthCheckDto
{
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public bool IsHealthy { get; set; }
    public List<ComponentHealth> Components { get; set; } = [];
}