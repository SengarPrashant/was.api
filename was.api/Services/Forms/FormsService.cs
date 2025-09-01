using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Auth;
using was.api.Models.Dtos;
using was.api.Models.Dtos.Forms;
using was.api.Models.Forms;
using was.api.Services.Coms;

namespace was.api.Services.Forms
{
    public class FormsService(ILogger<FormsService> logger, IEmailService emailService, AppDbContext dbContext, IOptions<Settings> options) : IFormsService
    {
        private AppDbContext _db = dbContext;
        private ILogger<FormsService> _logger = logger;
        private readonly Settings _settings = options.Value;
        private readonly IEmailService _emailService = emailService;

        public async Task<object?> GetFormDetails(string formType, string key)
        {
            try
            {
                // fetching form details
                var formDetails = await (
                               from field in _db.FormFields
                               join form in _db.FormDefinition on field.FormId equals form.Id
                               join section in _db.FormSections on field.SectionId equals section.Id
                               where form.FormType == formType && form.FormKey == key && section.IsActive ==true && field.IsActive ==true
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="user"></param>
        /// <returns>1: Success, 0: Area manager not registered, 2: EHS Manager not registered. -1: NA</returns>
        public async Task<int> SubmitForm(FormSubmissionRequest request, CurrentUser user)
        {
            request.Project = string.IsNullOrEmpty(request.Project) ? null : request.Project;

            DtoUser areaManger=new();
            DtoUser ehsManager = new();
            if (request.FormType == OptionTypes.work_permit)
            {
                areaManger = await _db.Users.Where(u => u.Zone == request.Zone && u.RoleId == (int)Constants.Roles.AreaManager && u.ActiveStatus == (int)Constants.UserStatus.Active).FirstOrDefaultAsync();
                if(areaManger == null || areaManger.Id==0) return 0;

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
                        FacilityZoneLocation = request.FacilityZoneLocation,
                        Zone = request.Zone,
                        ZoneFacility = request.ZoneFacility,
                        Project = request.Project
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

                     _ = ProcessWorkPermitEmail(formDto, areaManger);

                    return 1;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while submiiting the work permit form {request.ToJsonString()}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            else if(request.FormType == OptionTypes.incident)
            {
                ehsManager = await _db.Users.Where(u => u.Zone == request.Zone && u.RoleId == (int)Constants.Roles.EHSManager).FirstOrDefaultAsync();
                if (ehsManager == null || ehsManager.Id == 0) return 0;

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
                        FacilityZoneLocation = request.FacilityZoneLocation,
                        Zone = request.Zone,
                        ZoneFacility = request.ZoneFacility,
                        Project =request.Project
                    };

                    _ = ProcessIncidentEmail(formDto, ehsManager);
                    return -1;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error while submiiting the incident form {request.ToJsonString()}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return -1;
        }

        public async Task<int> UpdateForm(FormSubmissionRequest request, CurrentUser user)
        {
            var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var currentForm = await _db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == request.Id);
                if (currentForm == null) return 0;

                currentForm.FormData = JsonSerializer.Deserialize<JsonElement>(request.FormData);
                currentForm.FacilityZoneLocation = request.FacilityZoneLocation;
                currentForm.Zone = request.Zone;
                currentForm.ZoneFacility = request.ZoneFacility;
                currentForm.Project = request.Project;
                currentForm.UpdatedDate = DateTime.UtcNow;
                currentForm.UpdatedBy = user.Id;
                await _db.SaveChangesAsync();

                foreach (var file in request.Files)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);

                    var doc = new DtoFormDocument
                    {
                        FormSubmissionId = currentForm.Id,
                        FileName = file.FileName,
                        ContentType = file.ContentType ?? "application/octet-stream",
                        Content = Common.Compress(ms.ToArray())
                    };
                    await _db.FormDocuments.AddAsync(doc);
                }
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating the form {request.ToJsonString()}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<int> UpdateFormstatus(FormStatusUpdateRequest request, CurrentUser user)
        {
            var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var currentForm = await _db.FormSubmissions.FirstOrDefaultAsync(x => x.Id == request.Id);
                if (currentForm == null) return 0;
                currentForm.Status = request.Status;
                await _db.SaveChangesAsync();

                var wf = new DtoFormWorkFlowHistory {
                FormSubmissionId = currentForm.Id, ActionBy = user.Id, ActionDate =  DateTime.UtcNow, Remarks = request.Remarks, Status = request.Status
                };

                await _db.FormWorkFlow.AddAsync(wf);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating form status {request.ToJsonString()}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<FormResponse>> GetInbox(GetFormRequest request, CurrentUser user)
        {
            int _roleId = Convert.ToInt32(user.RoleId);
            var isRequestor = _roleId != (int)Constants.Roles.Admin && _roleId != (int)Constants.Roles.EHSManager && _roleId != (int)Constants.Roles.EHSManager;
            var isAreaManager = _roleId == (int)Constants.Roles.AreaManager;

            string sql = @"SELECT f.id, f.form_id,null as form_data, f.status, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.submitted_date > @p0
                         AND (@p1::bool IS FALSE OR f.submitted_by = @p2)
                         AND (@p3::bool IS FALSE OR f.zone = @p4)";

            var query = _db.FormSubmissionResult.FromSqlRaw(sql, DateTime.UtcNow.AddYears(-1),
                isRequestor, user.Id, isAreaManager, user.Zone)
                 .Select(f => new FormResponse
                 {
                     RequestId = Common.GenerateRequestId(f.FormType,f.Id),
                     Id = f.Id,
                     FormId = f.FormId,
                     FormData = f.FormData,
                     FormTitle = f.Title,
                     FormDes = f.Description,
                     FormType = f.FormType,
                     FormTypeKey = f.FormTypeKey,
                     ShortDesc = f.ShortDesc,
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

        public async Task<FormSubmissionDetail> RequestDetail(long id, CurrentUser user)
        {
            string sql = @"SELECT f.id, f.form_id,f.form_data, f.status, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,f.project,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.id = @p0";

            var query = _db.FormSubmissionResult.FromSqlRaw(sql, id)
                 .Select(f => new FormSubmissionDetail
                 {
                     RequestId = Common.GenerateRequestId(f.FormType, f.Id),
                     Id = f.Id,
                     FormId = f.FormId,
                     FormData = f.FormData,
                     FormTitle = f.Title,
                     FormDes = f.Description,
                     FormType = f.FormType,
                     FormTypeKey = f.FormTypeKey,
                     ShortDesc = f.ShortDesc,
                     Project = f.Project,
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

                     SubmittedDate = f.SubmittedDate,

                     SubmittedBy = new KeyVal
                     {
                         key = f.SubmittedBy.ToString(),
                         Value = _db.Users
                             .Where(u => u.Id == f.SubmittedBy)
                             .Select(u => u.FirstName + " " + u.LastName)
                             .FirstOrDefault()
                     },
                     Documents = _db.FormDocuments.Where(d=>d.FormSubmissionId == f.Id).Select(s=>new FormDocument {
                     Id=s.Id, FormSubmissionId=s.FormSubmissionId, ContentType= s.ContentType, FileName=s.FileName
                     }).ToList()
                     
                 });

            var results = await query.FirstOrDefaultAsync();

            return results;
        }

        public async Task<FormDocument?> Getdocument(long id, CurrentUser user)
        {
            var document = await _db.FormDocuments.Where(d => d.Id == id)
                .Select(s=>new FormDocument {
                     Id=s.Id, FormSubmissionId=s.FormSubmissionId, Content= Common.Decompress(s.Content) , ContentType= s.ContentType, FileName=s.FileName
                     }).FirstOrDefaultAsync();
            return document;
        }

        public async Task<bool> SubmisstionAllowed(string formType, string key, CurrentUser user)
        {
            string sql = @"SELECT count(*) AS Count
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE 
                         to_timestamp(jsonb_extract_path_text(f.form_data, 'formDetails', 'datetime_of_work_to'), 'YYYY-MM-DD""T""HH24:MI:SS')::timestamp < @p1
                         AND f.status <> @p2
                         AND f.submitted_by = @p3 AND  fd.form_type = @p4";
            using (var command = dbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                var p1 = command.CreateParameter();
                p1.ParameterName = "p1";
                p1.Value = DateTime.Now;

                var p2 = command.CreateParameter();
                p2.ParameterName = "p2";
                p2.Value = Convert.ToString(Convert.ToInt32(Constants.FormStatus.Closed));

                var p3 = command.CreateParameter();
                p3.ParameterName = "p3";
                p3.Value = user.Id;

                var p4 = command.CreateParameter();
                p4.ParameterName = "p4";
                p4.Value = formType;

                command.Parameters.Add(p1);
                command.Parameters.Add(p2);
                command.Parameters.Add(p3);
                command.Parameters.Add(p4);

                dbContext.Database.OpenConnection();
                int count = Convert.ToInt32(command.ExecuteScalar());
                return count == 0;
            }
            
        }

        private async Task ProcessWorkPermitEmail(DtoFormSubmissions dtoForm, DtoUser areaManger)
        {
            try
            {
           
                var result = _db.FormSubmissions.Where(x => x.Id == dtoForm.Id).Select(f => new {
                    FormDef = _db.FormDefinition.Where(o => o.Id == f.FormId).Select(u => new { 
                        u.Id, u.Title, Formtype=u.FormType
                    }).FirstOrDefault(),
                    f.SubmittedDate,
                    SubmittedBy = new KeyVal {
                        key = f.SubmittedBy.ToString(),
                        Value = _db.Users
                                 .Where(u => u.Id == f.SubmittedBy)
                                 .Select(u => u.FirstName + " " + u.LastName)
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
                }).FirstOrDefault();

                if (result == null) return;

                // var securityMail =  _db.SecurityMailConfigs.Where(x => x.ZoneId == dtoForm.Zone && x.ZoneFacilityId == dtoForm.ZoneFacility).FirstOrDefault();
                DtoSecurityMailConfig securityMail = null;
                var subject = $"{result.FormDef.Title}";
                var templateName = "L1_FM_to_AM";

                Dictionary<string, string> placeholders = new Dictionary<string, string>
                    {
                        { "WorkPermitName", result.FormDef.Title },
                        { "FacilityName", result.ZoneFacility.Value },
                        { "DateTime", result.SubmittedDate.ToISTString() },
                        { "Requester", result.SubmittedBy.Value }
                    };
                var toEmail = areaManger.Email;
                var cc = new List<string>();
                if (securityMail != null && !string.IsNullOrEmpty(securityMail.SecurityEmail)) cc.Add(securityMail.SecurityEmail);
               // cc.Add(_settings.DefaultSecurityEmail);
                await _emailService.SendTemplatedEmailAsync(toEmail, subject, templateName, placeholders, cc);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email for {dtoForm.ToJsonString()}");
            }
        }

        private async Task ProcessIncidentEmail(DtoFormSubmissions dtoForm, DtoUser ehsManger)
        {
            try
            {
                var result = _db.FormSubmissions.Where(x => x.Id == dtoForm.Id).Select(f => new {
                    FormDef = _db.FormDefinition.Where(o => o.Id == f.FormId).Select(u => new {
                        u.Id,
                        u.Title,
                        Formtype = u.FormType
                    }).FirstOrDefault(),
                    f.SubmittedDate,
                    SubmittedBy = new KeyVal
                    {
                        key = f.SubmittedBy.ToString(),
                        Value = _db.Users
                                 .Where(u => u.Id == f.SubmittedBy)
                                 .Select(u => u.FirstName + " " + u.LastName)
                                 .FirstOrDefault()
                    },
                    ZoneFacility = new KeyVal
                    {
                        key = f.ZoneFacility,
                        Value = _db.FormOptions
                                 .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility)
                                 .Select(o => o.OptionValue)
                                 .FirstOrDefault()
                    }
                }).FirstOrDefault();

                if (result == null) return;

                var areaManger = await _db.Users.Where(u => u.Zone == dtoForm.Zone && u.RoleId == (int)Constants.Roles.AreaManager && u.ActiveStatus == (int)Constants.UserStatus.Active).FirstOrDefaultAsync();
                // var securityMail = await _db.SecurityMailConfigs.Where(x => x.ZoneId == dtoForm.Zone && x.ZoneFacilityId == dtoForm.ZoneFacility).FirstOrDefaultAsync();
                DtoSecurityMailConfig securityMail = null;

                var cc = new List<string>();
                if (securityMail != null && !string.IsNullOrEmpty(securityMail.SecurityEmail)) cc.Add(securityMail.SecurityEmail);
                cc.Add(_settings.DefaultSecurityEmail);

                if (areaManger != null && !string.IsNullOrEmpty(areaManger.Email))
                    cc.Add(areaManger.Email);

                var subject = $"{result.FormDef.Title}";
                var templateName = "FM_to_EHS_Incident";

                Dictionary<string, string> placeholders = new Dictionary<string, string>
                    {
                        { "IncidentName", result.FormDef.Title },
                        { "FacilityName", result.ZoneFacility.Value },
                        { "DateTime", result.SubmittedDate.ToISTString() },
                        { "Reporter", result.SubmittedBy.Value }
                    };

                await _emailService.SendTemplatedEmailAsync(ehsManger.Email, subject, templateName, placeholders, cc);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email for {dtoForm.ToJsonString()}");
            }
        }

    }
}
