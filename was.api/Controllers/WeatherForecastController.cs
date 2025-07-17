using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using was.api.Models;
using was.api.Services.Auth;

namespace was.api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
       
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly Settings _settings;
        private readonly IUserContextService _userContextService;

        public WeatherForecastController(IUserContextService contextService, ILogger<WeatherForecastController> logger, IOptions<Settings> options)
        {
            _logger = logger;
            _settings = options.Value;
            _userContextService = contextService;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            _logger.LogError("Testing error");
            _logger.LogWarning("Testing warning");
            _logger.LogDebug("Testing debug");
            _logger.LogInformation("Testing info");
            var user = _userContextService.User;
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                
            })
            .ToArray();
        }
    }
}
