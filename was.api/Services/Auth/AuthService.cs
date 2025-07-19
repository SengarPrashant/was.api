using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using was.api.Models;
using was.api.Models.Auth;
using static System.Net.WebRequestMethods;
using static was.api.Helpers.Constants;

namespace was.api.Services.Auth
{
    public class AuthService(ILogger<AuthService> logger, AppDbContext dbContext, IOptions<Settings> options) : IAuthService
    {
        private AppDbContext _db = dbContext;
        private ILogger<AuthService> _logger= logger;
        private readonly Settings _settings = options.Value;
        private static readonly object _dummy = new();

        public string GetPasswordHash(string password) {
            var passwordHasher = new PasswordHasher<object>();
            return passwordHasher.HashPassword(_dummy, password);
        }
        public bool VerifyPassword(string passwordHash, string password)
        {
            var passwordHasher = new PasswordHasher<object>();
            var result = passwordHasher.VerifyHashedPassword(_dummy, passwordHash, password);
            return result == PasswordVerificationResult.Success;
        }
        public (string, string) GenerateToken(User user)
        {
            var now = DateTime.UtcNow;
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenKey = Encoding.UTF8.GetBytes(_settings.Jwt.SecretKey);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.SerialNumber, user.Id.ToString()),
                    new Claim(ClaimTypes.Surname, user.LastName),
                    new Claim(ClaimTypes.Name, user.FirstName),
                    new Claim(ClaimTypes.Role, user.RoleName) // For role-based auth
                ]),
                Expires = now.AddHours(12),
                Issuer = _settings.Jwt.Issuer,
                Audience = _settings.Jwt.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
            };
            var atoken = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(atoken);

            tokenDescriptor.Expires = now.AddDays(7);
            var rtoken = tokenHandler.CreateToken(tokenDescriptor);
            var refreshToken = tokenHandler.WriteToken(rtoken);
            return (accessToken, refreshToken);
        }

        public async Task<bool> SaveRefreshToken(int id, string refresToken)
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if(user is not null)
            {
                user.RefreshToken = refresToken;
                int rowsAff= await _db.SaveChangesAsync();
                return rowsAff > 0;
            }
            return false;
        }

        public async Task<bool> ChangePassword(ChangePasswordRequest request, CurrentUser currentUser)
        {
            try
            {
                var user = await _db.Users.Where(x => x.Id == request.Id && x.ActiveStatus == 1).FirstOrDefaultAsync();

                if (user == null) return false;
                if (VerifyPassword(user.Password, request.OldPassword.Trim()))
                {
                    user.Password = GetPasswordHash(request.NewPassword.Trim());
                    user.RefreshToken = null;
                    user.PasswordOtp = null;
                    user.UpdatedBy = currentUser.Id;
                    user.UpdatedDate = DateTime.UtcNow;
                    int rowsAff = await _db.SaveChangesAsync();
                    return rowsAff > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while changing password for: {request.Email}", ex);
                throw;
            }
        }

        public async Task<(string?, string?)> RefreshToken(RefreshTokenRequest request)
        {
            var principal = GetPrincipalToken(request.AccessToken, false);
            if(principal == null) return (null, null);
            // throw new SecurityTokenException("Invalid token");
            var userId = principal.Claims.First(x => x.Type == ClaimTypes.SerialNumber).Value;

            var user = await (from u in _db.Users
                              join r in _db.Roles
                              on u.RoleId equals r.Id
                              where u.Id == Convert.ToInt32(userId) && u.ActiveStatus == 1
                              select new User
                              {
                                  Id = u.Id,
                                  Email = u.Email,
                                  FirstName = u.FirstName,
                                  LastName = u.LastName,
                                  Password = u.Password,
                                  RefreshToken = request.RefreshToken,
                                  ActiveStatus = u.ActiveStatus,
                                  StatusName = ((UserStatus)u.ActiveStatus).ToString(),
                                  RoleId = u.RoleId,
                                  RoleName = r.Name
                              }).FirstOrDefaultAsync();

            if (user == null) return (null, null);

            if(user.RefreshToken != request.RefreshToken) return (null, null);

            var refreshPrincipal = GetPrincipalToken(request.RefreshToken);

            if(refreshPrincipal == null) return (null,  null);

            var (accessToken, refreshToken) = GenerateToken(user);

            if(await SaveRefreshToken(user.Id, refreshToken))
            {
                return (accessToken, refreshToken);
            }
            return (null, null);
        }

        public async Task<bool> GenerateOtp(ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.email)) return false;

                var user = await _db.Users.FirstOrDefaultAsync(x => x.ActiveStatus ==1 && x.Email.ToLower() == request.email.ToLower());
                if (user == null) return false;
                
                var length = 6;
                var otp = new Random().Next((int)Math.Pow(10, length - 1), (int)Math.Pow(10, length)).ToString();

                user.PasswordOtp = GetPasswordHash(otp);
                user.OtpCreatedAt = DateTime.UtcNow;
                user.RefreshToken = null;
                int rowsAff = await _db.SaveChangesAsync();
               
                return rowsAff > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while changing password for: {request.email}", ex);
                throw;
            }

        }

        public async Task<bool> ValidateOtpAndResetPassword(ResetPasswordRequest request)
        {
            if(string.IsNullOrEmpty(request.email) ||  string.IsNullOrEmpty(request.Otp) || string.IsNullOrEmpty(request.Password)) return false;

            var user = await _db.Users.FirstOrDefaultAsync(x => x.ActiveStatus == 1 && x.Email.ToLower() == request.email.ToLower());

            if (user == null || string.IsNullOrEmpty(user.PasswordOtp) || user.OtpCreatedAt ==null) return false;

            var timeSinceCreated = (DateTime.UtcNow - user.OtpCreatedAt).Value;

            if (VerifyPassword(user.PasswordOtp, request.Otp.Trim()) && timeSinceCreated.TotalSeconds <= _settings.OtpExpirySeconds)
            {
                user.Password = GetPasswordHash(request.Password.Trim());
                user.RefreshToken = null;
                user.PasswordOtp = null;
                user.OtpCreatedAt = null;
                int rowsAff = await _db.SaveChangesAsync();
                return rowsAff > 0;
            }

            return false;
        }
        
        private ClaimsPrincipal? GetPrincipalToken(string token, bool validateLifeTime =true)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = validateLifeTime, // 👈 Pass "false" to allow expired tokens
                ValidIssuer = _settings.Jwt.Issuer,
                ValidAudience = _settings.Jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Jwt.SecretKey))
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtToken || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256))
                return null;

            return principal;
        }

    }
}
