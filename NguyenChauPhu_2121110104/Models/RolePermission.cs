namespace NguyenChauPhu_2121110104.Models
{
    public class RolePermission
    {
        public int RolePermissionId { get; set; }
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public Role Role { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
}
