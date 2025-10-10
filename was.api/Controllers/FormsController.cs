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
        public async Task<IActionResult> SubmitForm([FromForm] FormSubmissionRequest request)
        {
            try
            {
                var user = _userContext.User;
                var res= await _formService.SubmitForm(request, user);

                if (res == 0) return BadRequest(new { message="Area manager not registered." });
                if (res == 2) return BadRequest(new { message = "EHS and Sustainability not registered." });

                return Ok(new {message="Form submitted successfully,"});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while saving form: {request.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [Authorize(Roles = "admin,e_m,pm_fm")]
        [HttpPost("update")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateForm([FromForm] FormSubmissionRequest request)
        {
            try
            {
                var user = _userContext.User;
                var res = await _formService.UpdateForm(request, user);

                if (res == 0) return BadRequest(new { message = "Area manager not registered." });
                if (res == 2) return BadRequest(new { message = "EHS and Sustainability not registered." });

                return Ok(new { message = "Form updated successfully," });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating form: {request.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost("inbox")]
        public async Task<IActionResult> MyInbox(GetFormRequest request)
        {
            var _user = _userContext.User;
            try
            {
                var (inboxList, statuscount) = await _formService.GetInbox(request, _user);
                return Ok(new { data=inboxList, meta=statuscount});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching inbox for user:{_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpGet("request/{id:long}")]
        public async Task<IActionResult> RequestDetail(long id)
        {
            var _user = _userContext.User;
            try
            {
                var res = await _formService.RequestDetail(id, _user);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching inbox for user:{_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpPost("updateStatus")]
        public async Task<IActionResult> UpdateStatus(FormStatusUpdateRequest request)
        {
            var _user = _userContext.User;
            try
            {
                var res = await _formService.UpdateFormstatus(request, _user);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating form status:{request.ToJsonString()}, user: {_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [AllowAnonymous]
        [HttpGet("document/{id:long}")]
        public async Task<IActionResult> GetDocument(long id)
        {
            var _user = _userContext.User;
            try
            {
                var document = await _formService.Getdocument(id, _user);

                if (document == null) return NotFound();

                return File(document.Content, document.ContentType, document.FileName);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching inbox for user:{_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }

        [HttpGet("prevalidate/{type}/{id}")]
        public async Task<IActionResult> PreValidate(string type, string id)
        {
            var _user = _userContext.User;
            try
            {
                var res = await _formService.SubmisstionAllowed(type, id, _user);
                return Ok(new { allowed = res });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching inbox for user:{_user.ToJsonString()}");
                return StatusCode(500, "Unknown error!");
            }
        }
    }
}
