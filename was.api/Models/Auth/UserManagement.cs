namespace was.api.Models.Auth
{
    public class UpdateUserRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? Mobile { get; set; }
        public int RoleId { get; set; }
    }
}
