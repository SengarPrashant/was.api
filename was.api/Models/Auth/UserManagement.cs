namespace was.api.Models.Auth
{
    public class UpdateUserRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? Mobile { get; set; }
        public string? EmployeeId { get; set; }
        public int RoleId { get; set; }
        public string FacilityZoneLocation { get; set; }
        public string Zone { get; set; }
        public string Facility { get; set; }
    }
    public class AdminResetPasswordRequest
    {
        public int Id { get; set; }
        public string NewPassword { get; set; }
    }
}
