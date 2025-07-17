﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Forms;
using was.api.Services.Auth;
using was.api.Services.Forms;

namespace was.api.Controllers
{
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
                _logger.LogError($"Error while getting form details for: {type}/{id}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }

        [HttpPost("options")]
        public async Task<IActionResult> GetFormOption(OptionsRequest request)
        {
            try
            {
                var res = await _formService.GetOptions(request);
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while getting options: {request.ToJsonString()}", ex);
                return StatusCode(500, "Something went wrong on the server.");
            }
        }
    }
}
