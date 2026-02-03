using Microsoft.EntityFrameworkCore;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Admin;
using was.api.Models.Auth;
using was.api.Services.Coms;
using static was.api.Helpers.Constants;

namespace was.api.Services.Auth
{
    public class UserManagementService(ILogger<UserManagementService> logger, IEmailService emailService, AppDbContext dbContext, IAuthService authService) : IUserManagementService
    {
        private AppDbContext _db = dbContext;
        private ILogger<UserManagementService> _logger = logger;
        private IAuthService _auth= authService;
        private IEmailService _emailService = emailService;

        public async Task<LoginResponse?> AuthenticateUser(LoginRequest request)
        {
            try
            {
                var res = new LoginResponse();
                // get user from db
                var user = await (from u in _db.Users
                                    join r in _db.Roles
                                    on u.RoleId equals r.Id
                                    where u.ActiveStatus == 1 && u.Email == request.email.ToLower()
                                    select new User
                                    {
                                        Id = u.Id,
                                        Email = u.Email,
                                        Mobile = u.Mobile,
                                        FirstName = u.FirstName,
                                        EmployeeId=u.EmployeeId,
                                        LastName = u.LastName,
                                        Password = u.Password,
                                        ActiveStatus = u.ActiveStatus,
                                        StatusName = ((UserStatus)u.ActiveStatus).ToString(),
                                        RoleId =   u.RoleId,
                                        RoleName = r.KeyName,
                                        RoleDisplay = r.Name,
                                        FacilityZoneLocation=u.FacilityZoneLocation,
                                        Zone = u.Zone,
                                        Facility = u.Facility
                                    }).FirstOrDefaultAsync();
                if (user is null) return null;

               // var hassgedPassword = _auth.GetPasswordHash(request.Password);
                if(_auth.VerifyPassword(user.Password, request.Password.Trim()))
                {
                    var (token, refreshToken) = _auth.GenerateToken(user);

                    if(await _auth.SaveRefreshToken(user.Id, refreshToken))
                    {
                        res.AccessToken = token;
                        res.RefreshToken = refreshToken;
                        user.Password = null;
                        user.PasswordOtp = null;
                        user.RefreshToken = null;
                        res.UserDetails = user;
                        return res;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while authenticating user: {request.email}", ex);
                throw;
            }
        }
        public async Task<User> CreateUser(User user, CurrentUser currentUser)
        {
            try
            {
                var userWithSameEmailOrMobile = await _db.Users
               .Where(x => x.Email == user.Email.ToLower() || x.EmployeeId.ToLower() == user.EmployeeId.ToLower().Trim() || x.Mobile == user.Mobile
                   )
               .FirstOrDefaultAsync();

                if(userWithSameEmailOrMobile != null)
                {
                    user.Id = -1;
                    return user;
                }

                var tempPwd = Common.GenerateTemporaryPassword(10);

                var newUser = new Models.Dtos.DtoUser {
                    Id = user.Id, Email = user.Email.Trim().ToLower(), 
                    FirstName = user.FirstName.Trim(), LastName=user.LastName.Trim(),
                    EmployeeId=user.EmployeeId,
                    Mobile = user.Mobile?.Trim(),
                    RoleId =user.RoleId,
                    ActiveStatus = 1, // active 
                    Password =_auth.GetPasswordHash(tempPwd),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUser.Id.ToString(),
                    FacilityZoneLocation=user.FacilityZoneLocation,
                    Zone=user.Zone,
                    Facility=user.Facility
                };
                _db.Users.Add(newUser);
                await _db.SaveChangesAsync();
               
                // reset sencitive info before returning
                user.Id = newUser.Id;
                user.Password = string.Empty;
                user.PasswordOtp = string.Empty; 
                user.RefreshToken = string.Empty;
                user.CreatedAt = DateTime.Now;

                Dictionary<string, string> placeholders = new Dictionary<string, string>
                {
                    { "USER", $"{user.FirstName}" },
                    { "TEMP_PASSWORD", $"{tempPwd}" }
                };

                await _emailService.SendTemplatedEmailAsync(user.Email, "User account created", "USER_CREATED", placeholders);

                return user;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error while creating user: {user.Email}");
                throw;
            }
        }

        public async Task<List<User>> FilterUsers(UserFilterRequest filter)
        {
            try
            {
                var query = from u in _db.Users
                            join r in _db.Roles on u.RoleId equals r.Id
                            select new
                            {
                                u,
                                RoleName = r.Name
                            };

                if (!string.IsNullOrEmpty(filter.FirstName))
                    query = query.Where(x => EF.Functions.Like(x.u.FirstName.ToLower(), $"%{filter.FirstName.Trim().ToLower()}%"));

                if (!string.IsNullOrEmpty(filter.LastName))
                    query = query.Where(x => EF.Functions.Like(x.u.LastName.ToLower(), $"%{filter.LastName.Trim().ToLower()}%"));

                if (!string.IsNullOrEmpty(filter.Email))
                    query = query.Where(x => EF.Functions.Like(x.u.Email, $"%{filter.Email.Trim().ToLower()}%"));

                if (!string.IsNullOrEmpty(filter.Role))
                    query = query.Where(x => x.RoleName == filter.Role);

                if (filter.ActiveStatus != null)
                    query = query.Where(x => x.u.ActiveStatus == filter.ActiveStatus);

                var orderBy = filter.OrderBy?.ToLower();
                var sortDirection = filter.ascending ? "asc" : "desc";

                query = (orderBy, sortDirection) switch
                {
                    ("firstname", "asc") => query.OrderBy(x => x.u.FirstName),
                    ("firstname", "desc") => query.OrderByDescending(x => x.u.FirstName),

                    ("lastname", "asc") => query.OrderBy(x => x.u.LastName),
                    ("lastname", "desc") => query.OrderByDescending(x => x.u.LastName),

                    ("email", "asc") => query.OrderBy(x => x.u.Email),
                    ("email", "desc") => query.OrderByDescending(x => x.u.Email),

                    ("mobile", "asc") => query.OrderBy(x => x.u.Mobile),
                    ("mobile", "desc") => query.OrderByDescending(x => x.u.Mobile),

                    _ => query.OrderByDescending(x => x.u.CreatedAt) // Default fallback
                };

                var users= await query
                    .OrderByDescending(x=>x.u.CreatedAt)
                    .Select(x =>
                            new User {
                                Id = x.u.Id,
                                FirstName = x.u.FirstName,
                                LastName = x.u.LastName,
                                Email = x.u.Email,
                                EmployeeId = x.u.EmployeeId,
                                Mobile = x.u.Mobile,
                                RoleId = x.u.RoleId,
                                RoleName = x.RoleName,
                                ActiveStatus = x.u.ActiveStatus,
                                StatusName = ((UserStatus)x.u.ActiveStatus).ToString(),
                                CreatedAt = x.u.CreatedAt,
                                FacilityZoneLocation=x.u.FacilityZoneLocation,
                                Zone=x.u.Zone,
                                Facility=x.u.Facility,
                            }).ToListAsync();
                return users;
            }
            catch (Exception e)
            {
                _logger.LogError(e,$"Error while filtering users: {filter.ToJsonString()}");
                throw;
            }
        }

        public async Task<bool> UpdateStatus(UpdateUserStatusRequest request, CurrentUser currentUser)
        {
            var user = await _db.Users.Where(x=>x.Id == request.Id).FirstOrDefaultAsync();
            if (user == null) return false;
            user.ActiveStatus = request.Status;

            var rowsAff = await _db.SaveChangesAsync();
            return rowsAff >0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="request"></param>
        /// <returns>
        /// 0:User not found, 1:Updated, 2:Duplicate email/mobile, 5:Unknown
        /// </returns>
        public async Task<int> UpdateUserDetails(int id, UpdateUserRequest request, CurrentUser currentUser)
        {
            var user = await _db.Users.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (user == null) return 0;

            var userWithSameEmailOrMobile = await _db.Users
                .Where(x => x.Id != id &&
                    (
                        x.Email == request.Email.ToLower() ||
                        (!string.IsNullOrEmpty(x.Mobile) && !string.IsNullOrEmpty(request.Mobile) && x.Mobile == request.Mobile)
                    ))
                .FirstOrDefaultAsync();

            if (userWithSameEmailOrMobile != null) return 2;

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = request.Email.Trim().ToLower();
            user.Mobile = request.Mobile;
            user.RoleId = request.RoleId;
            user.EmployeeId = request.EmployeeId;
            user.UpdatedBy = currentUser.Id;
            user.UpdatedDate = DateTime.UtcNow;
            user.FacilityZoneLocation = request.FacilityZoneLocation;
            user.Zone = request.Zone;
            user.Facility = request.Facility;

            var rowsAff = await _db.SaveChangesAsync();

            return rowsAff > 0 ? 1 : 5;
        }

        public async Task<bool> UpdatePasswordByAdmin(AdminResetPasswordRequest request, CurrentUser currentUser)
        {
            try
            {
                var user = await _db.Users.Where(x => x.Id == request.Id).FirstOrDefaultAsync();
                if (user == null) return false;

                user.UpdatedDate = DateTime.UtcNow;
                user.UpdatedBy = currentUser.Id;
                user.Password = _auth.GetPasswordHash(request.NewPassword);

                var rowsAff = await _db.SaveChangesAsync();
                return rowsAff > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating password by {currentUser.Email} for user {request.Id}");
                throw;
            }
        }
    }
}
