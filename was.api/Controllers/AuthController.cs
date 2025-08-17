using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using was.api.Models;
using was.api.Models.Auth;
using was.api.Services.Auth;
using was.api.Services.Coms;

namespace was.api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(ILogger<AuthController> logger, IOptions<Settings> options, 
        IUserManagementService userManagementService, IAuthService authService, IUserContextService userContext, IEmailService emailService) : ControllerBase
    {
        private readonly ILogger<AuthController> _logger = logger;
        private readonly Settings _settings = options.Value;
        private readonly IUserManagementService _userService = userManagementService;
        private readonly IUserContextService _userContext = userContext;
        private readonly IAuthService _authService= authService;
        private readonly IEmailService _emailService= emailService;

        
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"Received login request for user: {request.email}");

                var result = await _userService.AuthenticateUser(request);

                if (result == null) {
                    return Unauthorized("Inavlid username/password.");
                }
                return Ok(result);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while processing login for user: {request.email}");
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var (accessToken, refreshToken) = await _authService.RefreshToken(request);

                if (accessToken == null || refreshToken == null) { 
                    return Unauthorized("Invalid token!");
                }

                return Ok(new { accessToken, refreshToken });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing RefreshToken: {request.UserName}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
            
        }

        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (_userContext.User.Id != request.Id) return Unauthorized("Invalid request!");

                request.Email = _userContext.User.Email;
                request.Id = _userContext.User.Id;
                var updated = await _authService.ChangePassword(request, _userContext.User);
                if (updated) return Ok(new { Success = true});
                
                return Unauthorized("Invalid user!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while processing ChangePassword: {request.Email}");
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        [AllowAnonymous]
        [HttpPost("getOtp")]
        public async Task<IActionResult> GetOtp([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.email)) return BadRequest("Required parameters not supplied.");

                var user = await _authService.GenerateOtp(request);
                if (user == null) return Unauthorized("Invalid user!");
                // send email otpGenerated
                Dictionary<string, string> placeholders = new Dictionary<string, string>
                {
                    { "USER", $"{user.FirstName}" },
                    { "OTP_CODE", $"{user.PasswordOtp}" }
                };

                await _emailService.SendTemplatedEmailAsync(request.email.Trim(), "Your verification code", "Password_OTP", placeholders);

                return Ok(new { Success = true });

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing GetOtp: {request.email}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }
        [AllowAnonymous]
        [HttpPost("resetPassword")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var resetSucces = await _authService.ValidateOtpAndResetPassword(request);
                if (!resetSucces) return Unauthorized("Invalid user details!");
                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing ResetPassword: {request.email}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        [AllowAnonymous]
        [HttpPost("sendPasswordRequestEmail/{email}")]
        public async Task<IActionResult> Put([FromRoute] string email)
        {
            try
            {
                // send email to EHS
                Dictionary<string,string> placeholders = new Dictionary<string, string>
                {
                    { "WorkPermitName", "Confined work space" },
                    { "Sender", "Prashant" }
                };

                await _emailService.SendTemplatedEmailAsync(email, "Test", "EHS_to_AM_Reminder", placeholders);
               // var res = await _emailService.SendEmailAsync(email, "test", "<h1>hello</h1>");
                return Ok("We've notified the EHS team via email. Kindly reach out to them to obtain your new password.");
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
