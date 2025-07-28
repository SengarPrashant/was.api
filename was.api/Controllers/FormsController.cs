using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Forms;
using was.api.Services.Auth;
using was.api.Services.Forms;

namespace was.api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FormsController(ILogger<UserController> logger, IOptions<Settings> options,
        IFormsService formService, IUserContextService userContext) : ControllerBase
    {
        private readonly ILogger<UserController> _logger = logger;
        private readonly Settings _settings = options.Value;
        private readonly IFormsService _formService = formService;
        private readonly IUserContextService _userContext = userContext;

        // GET api/Forms/5
        [HttpGet("{type}/{id}")]
        public async Task<IActionResult> Get(string type, string id)
        {
            try
            {
                var user = _userContext.User;
                var res = await _formService.GetFormDetails(type, id);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while getting form details for: {type}/{id}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost("options")]
        public async Task<IActionResult> GetFormOption(OptionsRequest request)
        {
            try
            {
                if (request.OptionType.ToLower() == "roles")
                {
                    var roles = await _formService.GetRoles();
                    return Ok(roles);
                }
                var options = await _formService.GetOptions(request);
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while getting options: {request.ToJsonString()}", ex);
                return StatusCode(500, "Unknown error!");
            }
        }
        [HttpGet("options/All")]
        public async Task<IActionResult> GetFormOptionAll()
        {
            try
            {
                
                var options = await _formService.GetAllOptions();
                return Ok(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while fetching all options");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost("submit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitFomrm([FromForm] FormSubmissionRequest request)
        {
            try
            {
                var user = _userContext.User;
                var res= await _formService.SubmitForm(request, user);
                return Ok(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while saving form: {request.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost("inbox")]
        public async Task<IActionResult> MyInbox()
        {
            var _user = _userContext.User;
            try
            {
                var res = await _formService.GetFormList(new GetFormRequest(), _user);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching inbox for user:{_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }
    }
}
