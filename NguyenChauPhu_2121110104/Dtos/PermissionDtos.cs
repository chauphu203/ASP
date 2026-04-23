namespace NguyenChauPhu_2121110104.Dtos
{
    public record CreatePermissionRequest(string PermissionCode, string PermissionName, string ModuleName);
    public record AssignPermissionsRequest(List<int> PermissionIds);
    public record AssignRolesRequest(List<int> RoleIds);
}
