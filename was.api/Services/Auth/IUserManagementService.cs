using was.api.Models.Admin;
using was.api.Models.Auth;

namespace was.api.Services.Auth
{
    public interface IUserManagementService
    {
        public Task<LoginResponse?> AuthenticateUser(LoginRequest request);
        public Task<User> CreateUser(User user, CurrentUser currentUser);
        public Task<List<User>> FilterUsers(UserFilterRequest filter);
        public Task<bool> UpdateStatus(UpdateUserStatusRequest request, CurrentUser currentUser);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="request"></param>
        /// <returns>
        /// 0:User not found, 1:Updated, 2:Duplicate email/mobile, 5:Unknown
        /// </returns>
        public Task<int> UpdateUserDetails(int id, UpdateUserRequest request, CurrentUser currentUser);
        public Task<bool> UpdatePasswordByAdmin(AdminResetPasswordRequest request, CurrentUser currentUser);
    }
}
