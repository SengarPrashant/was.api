using Microsoft.AspNetCore.Identity;
using was.api.Models.Auth;

namespace was.api.Services.Auth
{
    public interface IAuthService
    {
        public Task<bool> ChangePassword(ChangePasswordRequest request, CurrentUser currentUser);
        public string GetPasswordHash(string password);
        public bool VerifyPassword(string passwordHash, string password);
        public (string, string) GenerateToken(User user);
        public Task<bool> SaveRefreshToken(int id, string refresToken);
        public Task<(string?, string?)> RefreshToken(RefreshTokenRequest request);
        public Task<User?> GenerateOtp(ResetPasswordRequest request);
        public Task<bool> ValidateOtpAndResetPassword(ResetPasswordRequest request);
    }
}
