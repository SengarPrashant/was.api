using was.api.Models.Auth;

namespace was.api.Services.Auth
{
    public interface IUserContextService
    {
       public CurrentUser? User { get; }
    }
}
