using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using was.api.Models;
using was.api.Models.Auth;
using was.api.Services.Auth;

namespace was.api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController(ILogger<AuthController> logger, IOptions<Settings> options, 
        IUserManagementService userManagementService, IAuthService authService, IUserContextService userContext) : ControllerBase
    {
        private readonly ILogger<AuthController> _logger = logger;
        private readonly Settings _settings = options.Value;
        private readonly IUserManagementService _userService = userManagementService;
        private readonly IUserContextService _userContext = userContext;
        private readonly IAuthService _authService= authService;

        //// GET: api/<AuthController>
        //[HttpGet]
        //public IEnumerable<string> Get()
        //{
        //    return new string[] { "value1", "value2" };
        //}

        //// GET api/<AuthController>/5
        //[HttpGet("{id}")]
        //public string Get(int id)
        //{
        //    return "value";
        //}

        // POST api/<AuthController>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"Received login request for user: {request.email}");

                var result = await _userService.AuthenticateUser(request);

                if (result == null) {
                    return Ok("Inavlid username/password.");
                }
                return Ok(result);
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing login for user: {request.email}", ex);
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
                request.Email = _userContext.User.Email;
                request.Id = _userContext.User.Id;
                var updated = await _authService.ChangePassword(request);
                if (updated) return Ok("Password updated. Please relogin");
                
                return Unauthorized("Invalid user!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing ChangePassword: {request.Email}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        [AllowAnonymous]
        [HttpPost("getOtp")]
        public async Task<IActionResult> GetOtp([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var otpGenerated = await _authService.GenerateOtp(request);
                if (!otpGenerated) return BadRequest("Invalid user!");

                return Ok("OTP sent to email!");

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
                return Ok("Password updated successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while processing ResetPassword: {request.email}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        //// PUT api/<AuthController>/5
        //[HttpPut("{id}")]
        //public void Put(int id, [FromBody] string value)
        //{
        //}

        //// DELETE api/<AuthController>/5
        //[HttpDelete("{id}")]
        //public void Delete(int id)
        //{
        //}

       

    }
}
