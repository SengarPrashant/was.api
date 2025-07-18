namespace was.api.Models.Admin
{
    public class UserFilterRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public int? RoleId { get; set; }
        public int? ActiveStatus { get; set; }

        public string? OrderBy { get; set; }
        public bool ascending { get; set; }

    }
    public class UpdateUserStatusRequest
    {
        public int Id { get; set; }
        public int Status { get; set; }
    }
}
