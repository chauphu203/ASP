namespace NguyenChauPhu_2121110104.Models
{
    public class UserRole
    {
        public int UserRoleId { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedBy { get; set; }

        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
}
