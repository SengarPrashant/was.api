using System.Security.Claims;
using was.api.Models.Auth;

namespace was.api.Services.Auth
{
    public class UserContextService(IHttpContextAccessor httpContext) : IUserContextService
    {
        private readonly IHttpContextAccessor _context = httpContext;

        public CurrentUser? User
        {
            get
            {
                var httpContext = _context.HttpContext;

                if (httpContext == null || httpContext.User == null)
                    return null;

                var user = httpContext.User;

                return new CurrentUser
                {
                    Id = Convert.ToInt32(user?.FindFirst(ClaimTypes.SerialNumber)?.Value ?? "0"),
                    Email = user?.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    Role = user?.FindFirst(ClaimTypes.Role)?.Value ?? "",
                    FirstName = user?.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    LastName = user?.FindFirst(ClaimTypes.Surname)?.Value ?? "",
                    IpAddress = (httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                                ?? httpContext.Connection?.RemoteIpAddress?.ToString()) ?? ""
                };
            }
        }
    }
}
