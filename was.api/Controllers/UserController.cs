using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using was.api.Models;
using was.api.Models.Admin;
using was.api.Models.Auth;
using was.api.Services.Auth;

namespace was.api.Controllers
{
    [Authorize(Roles ="Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController(ILogger<UserController> logger, IOptions<Settings> options, 
        IUserContextService userContextService,
        IUserManagementService userManagementService) : ControllerBase
    {
        private readonly ILogger<UserController> _logger = logger;
        private readonly Settings _settings = options.Value;
        private readonly IUserManagementService _userService = userManagementService;
        private readonly IUserContextService userContext = userContextService;

        [HttpPost("filter")]
        public async Task<IActionResult> Filter(UserFilterRequest filter)
        {
            try
            {
                var res = await _userService.FilterUsers(filter);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while fetching user list by {userContext.User.Email}", ex);
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] User request)
        {
            try
            {
                _logger.LogInformation($"Received login request for user: {request.Email}");

                var isValid = validateUser(request);
                if (!isValid) return BadRequest("Missing required fields");

                var result = await _userService.CreateUser(request, userContext.User);
                if (result.Id == -1) return BadRequest("User with same email/mobile already exists!");

                if (result == null || result.Id ==0)
                {
                    return BadRequest("Invalid user details!");
                }
                result.Id = 0;

                return Ok(result);

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while creating user: {request.Email}", ex);
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPatch("updateStatus")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateUserStatusRequest request)
        {
            try
            {
                _logger.LogInformation($"Received update status request for: {request} by {userContext.User.Email}");

                if (request.Status > 2) return BadRequest("Status not supported!");

                var success = await _userService.UpdateStatus(request, userContext.User);

                if (success) return Ok("Status updated!");

                return BadRequest("Invalid details!");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while updating status by user {userContext.User.Email}", ex);
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                _logger.LogInformation($"Received request to update user details: {request} by {userContext.User.Email}");
                var invalid = string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.FirstName)
                    || string.IsNullOrEmpty(request.LastName) || request.RoleId <= 0;

                if (invalid) return BadRequest("Missing required fields");

                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName))
                {
                    return BadRequest("Missing required fields.");
                }

                var status = await _userService.UpdateUserDetails(id,request, userContext.User);

                if (status == 0 || status == 5) BadRequest("Invalid user details!");

                if (status == 1) return Ok("User details updated!");

                if (status == 2) return BadRequest("Email/mobile is used by some other user!");

                return BadRequest("Invalid details!");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while updating userdetails by user {userContext.User.Email}", ex);
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPatch("resetPasswordByAdmin")]
        public async Task<IActionResult> UpdatePassword(AdminResetPasswordRequest request)
        {
            try
            {
                var updated = await _userService.UpdatePasswordByAdmin(request, userContext.User);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating password by admin.");
                return StatusCode(500, "Unknown error!");
            }
        }

        private bool validateUser(User user)
        {
            if(string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.EmployeeId) 
                || string.IsNullOrEmpty(user.FirstName) || string.IsNullOrEmpty(user.LastName) || user.RoleId <=0)
            {
                return false;
            }
            return true;
        }
    }
}
