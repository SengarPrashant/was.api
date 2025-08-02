using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text.Json;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Auth;
using was.api.Models.Dtos;
using was.api.Models.Dtos.Forms;
using was.api.Models.Forms;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace was.api.Services.Forms
{
    public class FormsService(ILogger<FormsService> logger, AppDbContext dbContext, IOptions<Settings> options) : IFormsService
    {
        private AppDbContext _db = dbContext;
        private ILogger<FormsService> _logger = logger;
        private readonly Settings _settings = options.Value;

        public async Task<object?> GetFormDetails(string formType, string key)
        {
            try
            {
                // fetching form details
                var formDetails = await (
                               from field in _db.FormFields
                               join form in _db.FormDefinition on field.FormId equals form.Id
                               join section in _db.FormSections on field.SectionId equals section.Id
                               where form.FormType == formType && form.FormKey == key
                               group new { field, form, section } by field.FormId into formGroup
                               select new
                               {
                                   FormId = formGroup.Key,
                                   formGroup.First().form.Title,
                                   formGroup.First().form.FormType,
                                   formGroup.First().form.Description,
                                   formGroup.First().form.FormKey,
                                   Sections = formGroup
                                       .GroupBy(x => x.section.Id)
                                       .OrderBy(g => g.First().section.Order)
                                       .Select(sectionGroup => new
                                       {
                                           SectionId = sectionGroup.Key,
                                           SectionTitle = sectionGroup.First().section.Title,
                                           sectionGroup.First().section.Order,
                                           Fields = sectionGroup
                                           .OrderBy(x => x.field.Order)
                                           .Select(x => new
                                           {
                                               x.field.Id,
                                               x.field.Label,
                                               x.field.FieldKey,
                                               x.field.Type,
                                               x.field.Placeholder,
                                               x.field.Order,
                                               x.field.OptionType,
                                               x.field.CascadeField,
                                               x.field.ColSpan,
                                               validations = _db.FormValidations.Where(v=> v.IsActive==true && v.FieldId == x.field.Id).Select(s=> new { type = s.Type, value=s.Value, message=s.Message }).ToList()
                                           }).ToList()
                                       }).ToList()
                               }
                           ).FirstOrDefaultAsync();


                // extracting unique option types
                //var optionTypes = formDetails?.Sections
                //                    .SelectMany(section => section.Fields)
                //                    .Select(field => field.OptionType)
                //                    .Where(optionType => optionType != null)
                //                    .Distinct()
                //                    .ToList();
                
                //var options = new List<OptionsResponse>();
                //if (optionTypes is not null)
                //{
                //    // fetching all the matching options
                //    options = await _db.FormOptions
                //                    .Where(x => optionTypes.Contains(x.OptionType))
                //                    .Select(x => new OptionsResponse
                //                    {
                //                        OptionType = x.OptionType,
                //                        OptionKey = x.OptionKey,
                //                        OptionValue = x.OptionValue,
                //                        CascadeType = x.CascadeType,
                //                        CascadeKey = x.CascadeKey,
                //                        IsActive = x.IsActive
                //                    }).ToListAsync();
                //}

                return new
                {

                    formDetails,
                    //options
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,$"Error while fetching form fields {formType}/{key}");
                throw;
            }
           
        }
        public async Task<List<OptionsResponse>> GetOptions(OptionsRequest request)
        {
            try
            {
                var query = _db.FormOptions.AsQueryable();
                query = query.Where(x => x.OptionType == request.OptionType && x.IsActive==true);

                if (!string.IsNullOrEmpty(request.CascadeType) && !string.IsNullOrEmpty(request.CascadeKey))
                    query = query.Where(x => x.CascadeType == request.CascadeType && x.CascadeKey == request.CascadeKey);

                var result = await query
                              .Select(x => new OptionsResponse
                              {
                                 // Id = x.Id,
                                  OptionType = x.OptionType,
                                  OptionKey = x.OptionKey,
                                  OptionValue = x.OptionValue,
                                  CascadeType = x.CascadeType,
                                  CascadeKey = x.CascadeKey,
                                  IsActive = x.IsActive
                              })
                              .ToListAsync();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching options {request.ToJsonString()}");
                throw;
            }
           
        }

        public async Task<List<OptionsResponse>> GetAllOptions()
        {
            try
            {
                var query = _db.FormOptions.AsQueryable();
                query = query.Where(x => x.IsActive == true);
                var result = await query
                              .Select(x => new OptionsResponse
                              {
                                  // Id = x.Id,
                                  OptionType = x.OptionType,
                                  OptionKey = x.OptionKey,
                                  OptionValue = x.OptionValue,
                                  CascadeType = x.CascadeType,
                                  CascadeKey = x.CascadeKey,
                                  IsActive = x.IsActive
                              })
                              .ToListAsync();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while fetching all options");
                throw;
            }
        }

        public async Task<IEnumerable<DtoRoles>> GetRoles()
        {
            try
            {
                var roles = await _db.Roles.Where(x => x.IsActive == true).ToListAsync();
                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,"Error while fetching roles");
                throw;
            }
        }

        public async Task<bool> SubmitForm(FormSubmissionRequest request, CurrentUser user)
        {
            //using var transaction = await _db.Database.BeginTransactionAsync();
            var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var formDto = new DtoFormSubmissions
                {
                    FormId = request.FormId,
                    FormData = JsonSerializer.Deserialize<JsonElement>(request.FormData),
                    Status = request.Status,
                    SubmittedBy = user.Id,
                    SubmittedDate = DateTime.UtcNow,
                    FacilityZoneLocation=request.FacilityZoneLocation,
                    Zone=request.Zone,
                    ZoneFacility=request.ZoneFacility
                };

                await _db.FormSubmissions.AddAsync(formDto);
                await _db.SaveChangesAsync();

                foreach (var file in request.Files)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);

                    var doc = new DtoFormDocument
                    {
                        FormSubmissionId = formDto.Id,
                        FileName = file.FileName,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        Content = Common.Compress(ms.ToArray())
                    };
                    await _db.FormDocuments.AddAsync(doc);
                }
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while submiiting teh form {request.ToJsonString()}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<FormResponse>> GetFormList(GetFormRequest request, CurrentUser user)
        {
            int _roleId = Convert.ToInt32(user.RoleId);

            var isRequestor = _roleId != (int)Constants.Roles.Admin && _roleId != (int)Constants.Roles.EHSManager && _roleId != (int)Constants.Roles.EHSManager;

            var isAreaManager = _roleId == (int)Constants.Roles.AreaManager;
            
            var query = _db.FormSubmissions
                 .Where(f => f.SubmittedDate > DateTime.UtcNow.AddYears(-1))
                 .Select(f => new FormResponse
                 {
                     Id = f.Id,
                     FormId = f.FormId,
                     FormData = f.FormData,

                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     FacilityZoneLocation = new KeyVal
                     {
                         key = f.FacilityZoneLocation,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.FacilityZoneLocation && o.OptionType == OptionTypes.facility_zone_location)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     DocumentCount = _db.FormDocuments
                         .Where(d => d.FormSubmissionId == f.Id)
                         .Count(),

                     SubmittedDate = f.SubmittedDate,

                     SubmittedBy = new KeyVal
                     {
                         key = f.SubmittedBy.ToString(),
                         Value = _db.Users
                             .Where(u => u.Id == f.SubmittedBy)
                             .Select(u => u.FirstName + " " + u.LastName)
                             .FirstOrDefault()
                     },
            //         LatestWorkflowStatus = _db.FormWorkflows
            //.Where(wf => wf.FormSubmissionId == f.Id)
            //.OrderByDescending(wf => wf.UpdatedDate) // or wf.Id
            //.Select(wf => wf.Status)
            //.FirstOrDefault(),

                 });

            query = query.WhereIf(isAreaManager, f => f.Zone.key == user.Zone);
            query = query.WhereIf(isRequestor, f => f.SubmittedBy.key == user.Id.ToString());

            var results = await query.Distinct().ToListAsync();
            
            return results;
        }

        public async Task<List<FormResponse>> GetInbox(GetFormRequest request, CurrentUser user)
        {
            int _roleId = Convert.ToInt32(user.RoleId);
            var isRequestor = _roleId != (int)Constants.Roles.Admin && _roleId != (int)Constants.Roles.EHSManager && _roleId != (int)Constants.Roles.EHSManager;
            var isAreaManager = _roleId == (int)Constants.Roles.AreaManager;

            string shortDescPath = "{formData,formDetails,title}"; // Adjust to match your actual JSON path!
            // f.""form_data"" #>> '{shortDescPath}' AS ""ShortDesc""
            string sql = @$" SELECT f.""id"",  f.""form_id"", f.""form_data"",  f.""status"", f.""zone_facility"",
                                f.""zone"", f.""facility_zone_location"", f.""submitted_date"", f.""submitted_by""
                            FROM ""form_submissions"" f
                            WHERE f.""submitted_date"" > @p0
                            AND (@p1::bool IS FALSE OR f.""submitted_by"" = @p2)
                            AND (@p3::bool IS FALSE OR f.""zone"" = @p4)
                        ";
            var query = _db.FormSubmissions.FromSqlRaw(sql, DateTime.UtcNow.AddYears(-1),
                isRequestor, user.Id, isAreaManager, user.Zone)
                 .Select(f => new FormResponse
                 {
                     Id = f.Id,
                     FormId = f.FormId,
                     FormData = f.FormData,

                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     FacilityZoneLocation = new KeyVal
                     {
                         key = f.FacilityZoneLocation,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.FacilityZoneLocation && o.OptionType == OptionTypes.facility_zone_location)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     DocumentCount = _db.FormDocuments
                         .Where(d => d.FormSubmissionId == f.Id)
                         .Count(),

                     SubmittedDate = f.SubmittedDate,

                     SubmittedBy = new KeyVal
                     {
                         key = f.SubmittedBy.ToString(),
                         Value = _db.Users
                             .Where(u => u.Id == f.SubmittedBy)
                             .Select(u => u.FirstName + " " + u.LastName)
                             .FirstOrDefault()
                     }
                 });

            var results = await query.Distinct().ToListAsync();

            return results;
        }
    }
}
