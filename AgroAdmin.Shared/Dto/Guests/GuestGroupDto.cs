namespace AgroAdmin.Shared.Dto.Guests
{
    public class GuestGroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MembersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
