

namespace was.api.Models.Auth
{
    public class LoginRequest
    {
        public string email { get; set; }
        public string Password { get; set; }
    }
    public class LoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public User UserDetails { get; set; }
    }

    public class ChangePasswordRequest
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
    public class ResetPasswordRequest
    {
        public string email { get; set; }
        public string? Otp { get; set; }
        public string? Password { get; set; }
    }
    public class User
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? Mobile { get; set; }
        public string Password { get; set; }
        public string? PasswordOtp { get; set; } = string.Empty;
        public string? RefreshToken { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string? RoleName { get; set; } = string.Empty;
        /// <summary>
        /// 0:Deleted, 1:Active, 2:Deactivated, 3:Locked
        /// </summary>
        public int ActiveStatus { get; set; }
        public string? StatusName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FacilityZoneLocation { get; set; }
        public string Zone { get; set; }
        public string Facility { get; set; }
    }

    public class CurrentUser
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string RoleId { get; set; }
        public string IpAddress { get; set; }
        public string FacilityZoneLocation { get; set; }
        public string Zone { get; set; }
        public string Facility { get; set; }
    }
}
