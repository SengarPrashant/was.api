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
using static was.api.Helpers.Constants;

namespace was.api.Services.Forms
{
    public class FormsService(ILogger<FormsService> logger, IEmailService emailService, 
        AppDbContext dbContext, IOptions<Settings> options) : IFormsService
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
                                           sectionGroup.First().section.SectionStyle,
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
                                               x.field.Prefix,
                                               x.field.Suffix,
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
                        Status = Convert.ToInt32(Constants.FormStatus.Pending).ToString(),
                        PendingWith = (int)Constants.Roles.AreaManager,
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
                ehsManager = await _db.Users.Where(u => u.Zone == request.Zone && u.RoleId == (int)Roles.EHSManager).FirstOrDefaultAsync();
                if (ehsManager == null || ehsManager.Id == 0) return 0;

                areaManger = await _db.Users.Where(u => u.Zone == request.Zone && u.RoleId == (int)Roles.AreaManager && u.ActiveStatus == (int)UserStatus.Active).FirstOrDefaultAsync();

                var transaction = await _db.Database.BeginTransactionAsync();
                try
                {
                    var formDto = new DtoFormSubmissions
                    {
                        FormId = request.FormId,
                        FormData = JsonSerializer.Deserialize<JsonElement>(request.FormData),
                        Status = Convert.ToInt32(Constants.FormStatus.Pending).ToString(),
                        PendingWith = (int)Constants.Roles.EHSManager,
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

                    _ = ProcessIncidentEmail(formDto, ehsManager, areaManger);
                    return 1;
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

                var formDef = await _db.FormDefinition.FirstOrDefaultAsync(x => x.Id == currentForm.FormId);
                string emailFlag = "NA";
                if(formDef.FormType == OptionTypes.work_permit)
                {
                    var wf = new DtoFormWorkFlowHistory
                    {
                        FormSubmissionId = currentForm.Id,
                        ActionBy = user.Id,
                        ActionDate = DateTime.UtcNow,
                        Remarks = request.Remarks,
                        Status = request.Status
                    };

                    await _db.FormWorkFlow.AddAsync(wf);
                    await _db.SaveChangesAsync();

                    var areaManager = Convert.ToInt32(Constants.Roles.AreaManager).ToString();
                    var ehsManager = Convert.ToInt32(Constants.Roles.EHSManager).ToString();
                    var admin = Convert.ToInt32(Constants.Roles.Admin).ToString();
                    var fm = Convert.ToInt32(Constants.Roles.ProjectManager_FacilityManager).ToString();

                    var isRejected = request.Status == Convert.ToString((int)Constants.FormStatus.Rejected);

                    if (user.RoleId == areaManager)
                    {
                       currentForm.Status = isRejected ? Convert.ToString((int)Constants.FormStatus.Rejected) : currentForm.Status;
                       currentForm.PendingWith = isRejected ? (int)Constants.Roles.ProjectManager_FacilityManager : (int)Constants.Roles.EHSManager;
                       emailFlag = isRejected ? "AM_REJECTED" : "AM_APPROVED";
                    }
                    else if (user.RoleId == admin || user.RoleId == ehsManager)
                    {
                       currentForm.PendingWith = Convert.ToInt32(Constants.Roles.ProjectManager_FacilityManager);
                       currentForm.Status = isRejected ? Convert.ToString((int)Constants.FormStatus.Rejected) : Convert.ToInt32((int)Constants.FormStatus.Approved).ToString();
                       emailFlag = isRejected ? "EHS_REJECTED" : "EHS_APPROVED";
                    }
                    else if (user.RoleId == fm)
                    {
                        currentForm.Status = request.Status;
                        emailFlag = request.Status == ((int)Constants.FormStatus.Closed).ToString() ? "FM_CLOSED" : "NA";
                    }

                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();
                    if (emailFlag != "NA")
                    {
                        try
                        {
                            await ProcessStatusUpdateEmail(emailFlag, request, user, currentForm);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error sending status email for form {currentForm.Id}");
                        }
                    }

                    return 1;
                }
                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating form status {request.ToJsonString()}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task ProcessStatusUpdateEmail(string flag, FormStatusUpdateRequest status, CurrentUser currentUser, DtoFormSubmissions dtoForm)
        {
            var mapping = new Dictionary<string, (string SubjectSuffix, string TemplateName)>
                    {
                        { "AM_APPROVED", ("L1 Approved", "L1_AM_Approved") },
                        { "AM_REJECTED", ("L1 Rejected", "L1_AM_Rejected") },
                        { "EHS_APPROVED", ("L2 Approved", "L2_EHS_Approved") },
                        { "EHS_REJECTED", ("L2 Rejected", "L2_EHS_Rejected") },
                        { "FM_CLOSED", ("Closed", "FM_CLOSED") }
                    };

            if (!mapping.TryGetValue(flag, out var config))
                return;

            var ccEmails = await GetCCEmails(flag,currentUser, dtoForm);
            var toEmails = await GetToEmails(flag, currentUser, dtoForm);

            await SendStatusEmailAsync(status, currentUser, dtoForm, config.SubjectSuffix, config.TemplateName, toEmails, ccEmails);
        }

        private async Task<List<string>> GetToEmails(string flag,CurrentUser currentUser, DtoFormSubmissions dtoForm)
        {
            return flag switch
            {
                "AM_APPROVED" or "FM_CLOSED"
                    => await (from u in _db.Users
                              join r in _db.Roles on u.RoleId equals r.Id
                              where r.Id == (int)Constants.Roles.EHSManager
                              select u.Email).ToListAsync(),

                "AM_REJECTED" or "EHS_APPROVED" or "EHS_REJECTED"
                    => await _db.Users.Where(x => x.Id == dtoForm.SubmittedBy).Select(s => s.Email).ToListAsync(),

                _ => new List<string>()
            };
           
        }

        private async Task<List<string>?> GetCCEmails(string flag, CurrentUser currentUser, DtoFormSubmissions dtoForm)
        {
            var amEmails = await _db.Users.Where(x=>x.RoleId == (int)Constants.Roles.AreaManager && x.Zone==dtoForm.Zone && x.ActiveStatus == 1).Select(x => x.Email).ToListAsync(); ;

            List<string> securityEmail = null;
            if (_settings.EnableSecutyEmail)
            {
                securityEmail = await _db.SecurityMailConfigs.Where(x => x.ZoneId == dtoForm.Zone && x.ZoneFacilityId == dtoForm.ZoneFacility && x.IsActive == true).Select(x => x.SecurityEmail).ToListAsync();
            }

            var ccList = new List<string>();
            if(amEmails is not null)
            {
                ccList.AddRange(amEmails);
            }
            if (securityEmail is not null) {
                ccList.AddRange(securityEmail);
            }

            if(flag == "AM_REJECTED")
            {
               var ehsEmails = await (from u in _db.Users
                                      join r in _db.Roles on u.RoleId equals r.Id
                                      where r.Id == (int)Roles.EHSManager && u.ActiveStatus == (int)UserStatus.Active
                                      select u.Email).ToListAsync();
                if (ehsEmails is not null && ehsEmails.Count != 0)
                {
                    ccList.AddRange(ehsEmails);
                }
            }

            return ccList.Count > 0 ? ccList : null;
        }

        private async Task SendStatusEmailAsync(FormStatusUpdateRequest status, CurrentUser currentUser, DtoFormSubmissions dtoForm, string subjectSuffix, 
            string templateName, List<string> toList, List<string> ccList=null)
        {
            var result = _db.FormSubmissions
                .Where(x => x.Id == dtoForm.Id)
                .Select(f => new
                {
                    FormDef = _db.FormDefinition
                        .Where(o => o.Id == f.FormId)
                        .Select(u => new { u.Id, u.Title, Formtype = u.FormType })
                        .FirstOrDefault(),
                    f.SubmittedDate,
                    ZoneFacility = new KeyVal
                    {
                        key = f.ZoneFacility,
                        Value = _db.FormOptions
                            .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility 
                            && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                            .Select(o => o.OptionValue)
                            .FirstOrDefault()
                    }
                }).FirstOrDefault();

                if (result == null) return;

                var subject = $"{result.FormDef.Title} {subjectSuffix}";

            var actionBy = await _db.Users.Where(x => x.Id == currentUser.Id && x.ActiveStatus == (int)UserStatus.Active).FirstOrDefaultAsync() ;
            if (actionBy != null)
            {
                var placeholders = new Dictionary<string, string>
                {
                    { "WorkPermitName", result.FormDef.Title },
                    { "FacilityName", result.ZoneFacility.Value },
                    { "DateTime", result.SubmittedDate.ToISTString() },
                    { "ActionBy", $"{actionBy.FirstName} {actionBy.LastName}" },
                    { "Contact", string.IsNullOrEmpty(actionBy.Mobile) ? "" : actionBy.Mobile },
                    { "Email", actionBy.Email },
                    { "Remarks", string.IsNullOrEmpty(status.Remarks) ? "NA" : status.Remarks },
                };
                var toEmail = string.Join(",", toList);
                await _emailService.SendTemplatedEmailAsync(toEmail, subject, templateName, placeholders, ccList);
            }
        }
       
        public async Task<(List<FormResponse>, List<StatusCount>)> GetInbox(GetFormRequest request, CurrentUser user)
        {
           if(request.FormType == OptionTypes.work_permit)
            {
                var result = await GetWorkPermitList(request,user);
                return result;
            }
            else if(request.FormType == OptionTypes.incident)
            {
                var result = await GetIncidentList(request, user);
                return result;
            }
            return ([], []);
        }

        private async Task<(List<FormResponse>, List<StatusCount>)> GetWorkPermitList(GetFormRequest request, CurrentUser user)
        {
            int _roleId = Convert.ToInt32(user.RoleId);
            var isRequestor = _roleId == (int)Constants.Roles.ProjectManager_FacilityManager;
            var isAreaManager = _roleId == (int)Constants.Roles.AreaManager;

            var isAdminOrEHS = _roleId == (int)Constants.Roles.Admin || _roleId == (int)Constants.Roles.EHSManager;

            DateTime? fromDateUtc = request.FromDate?.ToUniversalTime();
            DateTime? toDateUtc = request.ToDate?.ToUniversalTime();

            string sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.submitted_date > @p0
                         AND (@p1::bool IS FALSE OR f.submitted_by = @p2)
                         AND (@p3::bool IS FALSE OR f.zone = @p4)
                         AND fd.form_type = @p4";

            IQueryable<FormResponse> query = null;
            // ,jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
            if (isRequestor)
            {
                sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         null as short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.submitted_date >= COALESCE({0}, (NOW() AT TIME ZONE 'UTC' - interval '1 year'))
                           AND f.submitted_date <= COALESCE({1}, (NOW() AT TIME ZONE 'UTC'))
                         AND f.submitted_by ={2}
                         AND fd.form_type = {3}";

                query = _db.FormSubmissionResult.FromSqlRaw(sql, (object?)fromDateUtc ?? DBNull.Value, (object?)toDateUtc ?? DBNull.Value, 
                    user.Id, request.FormType.ToLower())
                 .Select(f => new FormResponse
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
                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },
                     PendingWith = new KeyVal
                     {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },
                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility 
                             && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
            }

            if (isAdminOrEHS)
            {
                sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         null as short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE 
                         f.submitted_date >= COALESCE({0}, (NOW() AT TIME ZONE 'UTC' - interval '1 year'))
                           AND f.submitted_date <= COALESCE({1}, (NOW() AT TIME ZONE 'UTC'))
                         AND f.pending_with <> {2} AND fd.form_type = {3}";

                query = _db.FormSubmissionResult.FromSqlRaw(sql, (object?)fromDateUtc ?? DBNull.Value, (object?)toDateUtc ?? DBNull.Value,
                (int)Constants.Roles.AreaManager, request.FormType.ToLower())
                 .Select(f => new FormResponse
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
                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },
                     PendingWith = new KeyVal
                     {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },
                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                              && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
            }

            if (isAreaManager)
            {
                sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                          null as short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE
                         f.submitted_date >= COALESCE({0}, (NOW() AT TIME ZONE 'UTC' - interval '1 year'))
                           AND f.submitted_date <= COALESCE({1}, (NOW() AT TIME ZONE 'UTC'))
                         
                         AND f.zone = {2} AND fd.form_type = {3}";
                // AND f.pending_with ={2} (int)Constants.Roles.AreaManager,
                query = _db.FormSubmissionResult.FromSqlRaw(sql, (object?)fromDateUtc ?? DBNull.Value, (object?)toDateUtc ?? DBNull.Value,
                 user.Zone, request.FormType.ToLower())
                 .Select(f => new FormResponse
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
                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },
                     PendingWith = new KeyVal
                     {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },
                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                              && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
            }


            var results = await query.Distinct().OrderByDescending(f => f.SubmittedDate).ToListAsync();

            var allStatuses = await _db.FormOptions
                .Where(o => o.OptionType == OptionTypes.form_status)
                .ToListAsync();

            var allFormTypes = new List<string> { OptionTypes.work_permit, OptionTypes.incident };

            var statusCounts = (
                from ft in allFormTypes
                from st in allStatuses
                let count = results.Count(r => r.FormType == ft && r.Status.key == st.OptionKey)
                select new StatusCount { FormType = ft, Status = st.OptionValue, Count = count }
            ).ToList();

            return (results, statusCounts);
        }

        private async Task<(List<FormResponse>, List<StatusCount>)> GetIncidentList(GetFormRequest request, CurrentUser user)
        {
            int _roleId = Convert.ToInt32(user.RoleId);
            var isRequestor = _roleId == (int)Constants.Roles.ProjectManager_FacilityManager;
            var isAreaManager = _roleId == (int)Constants.Roles.AreaManager;

            var isAdminOrEHS = _roleId == (int)Constants.Roles.Admin || _roleId == (int)Constants.Roles.EHSManager;

            DateTime? fromDateUtc = request.FromDate?.ToUniversalTime();
            DateTime? toDateUtc = request.ToDate?.ToUniversalTime();

            string sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.submitted_date > @p0
                         AND (@p1::bool IS FALSE OR f.submitted_by = @p2)
                         AND (@p3::bool IS FALSE OR f.zone = @p4)
                         AND f.form_type = @p5";

            IQueryable<FormResponse> query = null;
            // ,jsonb_extract_path_text(f.form_data, 'formDetails', 'work_description') AS short_desc
            if (isRequestor)
            {
                sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         null as short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE f.submitted_date >= COALESCE({0}, (NOW() AT TIME ZONE 'UTC' - interval '1 year'))
                           AND f.submitted_date <= COALESCE({1}, (NOW() AT TIME ZONE 'UTC'))
                         AND f.submitted_by ={2}
                         AND fd.form_type = {3}";

                query = _db.FormSubmissionResult.FromSqlRaw(sql, (object?)fromDateUtc ?? DBNull.Value, (object?)toDateUtc ?? DBNull.Value,
                    user.Id, request.FormType.ToLower())
                 .Select(f => new FormResponse
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
                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },
                     PendingWith = new KeyVal
                     {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },
                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                             && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
            }

            else if (isAdminOrEHS)
            {
                sql = @"SELECT f.id, f.form_id,null as form_data, f.status,f.pending_with, f.zone_facility,
                         f.zone, f.facility_zone_location, f.submitted_date, f.submitted_by,
                         fd.title, fd.desc AS desc, fd.form_type, fd.form_type_key,
                         null as short_desc
                         FROM form_submissions f INNER JOIN form_def fd ON f.form_id = fd.id
                         WHERE 
                         f.submitted_date >= COALESCE({0}, (NOW() AT TIME ZONE 'UTC' - interval '1 year'))
                           AND f.submitted_date <= COALESCE({1}, (NOW() AT TIME ZONE 'UTC'))
                         AND fd.form_type = {2}";

                query = _db.FormSubmissionResult.FromSqlRaw(sql, (object?)fromDateUtc ?? DBNull.Value,
                    (object?)toDateUtc ?? DBNull.Value,
                request.FormType.ToLower())
                 .Select(f => new FormResponse
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
                     Status = new KeyVal
                     {
                         key = f.Status,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Status && o.OptionType == OptionTypes.form_status)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },
                     PendingWith = new KeyVal
                     {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },
                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                             && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
            }
            else
            {
                return (new List<FormResponse>(), new List<StatusCount>());
            }

            var results = await query.Distinct().OrderByDescending(f => f.SubmittedDate).ToListAsync();

            //var allStatuses = await _db.FormOptions
            //    .Where(o => o.OptionType == OptionTypes.form_status)
            //    .ToListAsync();

            var incTypes = await _db.FormOptions
               .Where(o => o.OptionType == OptionTypes.incident)
               .ToListAsync();

            var allFormTypes = new List<string> { OptionTypes.incident };

            var statusCounts = (
                from ft in allFormTypes
                from st in incTypes
                let count = results.Count(r => r.FormType == ft && Convert.ToString(r.FormTypeKey) == st.OptionKey)
                select new StatusCount { FormType = ft, Status = st.OptionValue, Count = count }
            ).ToList();

            return (results, statusCounts);
        }

        public async Task<FormSubmissionDetail> RequestDetail(long id, CurrentUser user)
        {
            string sql = @"SELECT f.id, f.form_id,f.form_data, f.status,f.pending_with, f.zone_facility,
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

                     PendingWith = new KeyVal {
                         key = f.PendingWith.ToString(),
                         Value = _db.Roles.FirstOrDefault(x => x.Id == f.PendingWith).Name
                     },

                     ZoneFacility = new KeyVal
                     {
                         key = f.ZoneFacility,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility
                             && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                             .Select(o => o.OptionValue)
                             .FirstOrDefault()
                     },

                     Zone = new KeyVal
                     {
                         key = f.Zone,
                         Value = _db.FormOptions
                             .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                             && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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
                     Documents = _db.FormDocuments.Where(d => d.FormSubmissionId == f.Id).Select(s => new FormDocument {
                         Id = s.Id, FormSubmissionId = s.FormSubmissionId, ContentType = s.ContentType, FileName = s.FileName
                     }).ToList()

                 });

            var result = await query.FirstOrDefaultAsync();

            var wfQuery = from wf in _db.FormWorkFlow
                          join fo in _db.FormOptions on wf.Status equals fo.OptionKey
                          join u in _db.Users on wf.ActionBy equals u.Id
                          where fo.OptionType == OptionTypes.form_status
                          && wf.FormSubmissionId== result.Id orderby wf.ActionDate
                          select new FormWfResponse
                          {
                              ActionDate = wf.ActionDate.ToISTString(),
                              ActionBy = $"{u.FirstName} {u.LastName}",
                              Action = fo.OptionValue,
                              Remarks= wf.Remarks
                          };

            var wfResult = await wfQuery.ToListAsync();

            result.Workflow = wfResult;

            return result;
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
                         AND f.status not in (@p2,@p3)
                         AND f.submitted_by = @p4 AND  fd.form_type = @p5";
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
                p3.Value = Convert.ToString(Convert.ToInt32(Constants.FormStatus.Rejected));

                var p4 = command.CreateParameter();
                p4.ParameterName = "p4";
                p4.Value = user.Id;

                var p5 = command.CreateParameter();
                p5.ParameterName = "p5";
                p5.Value = formType;

                command.Parameters.Add(p1);
                command.Parameters.Add(p2);
                command.Parameters.Add(p3);
                command.Parameters.Add(p4);
                command.Parameters.Add(p5);

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
                                 .Select(u => u.FirstName + " " + u.LastName + "|" + u.Email+"|" + (string.IsNullOrEmpty(u.Mobile) ? "" : u.Mobile))
                                 .FirstOrDefault()
                    },
                    ZoneFacility = new KeyVal
                    {
                        key = f.ZoneFacility,
                        Value = _db.FormOptions
                                 .Where(o => o.OptionType == OptionTypes.zone_facility && o.OptionKey == f.ZoneFacility
                                 && o.CascadeType== OptionTypes.zone && o.CascadeKey == f.Zone)
                                 .Select(o => o.OptionValue)
                                 .FirstOrDefault()
                    },
                    Zone = new KeyVal
                    {
                        key = f.Zone,
                        Value = _db.FormOptions
                                 .Where(o => o.OptionKey == f.Zone && o.OptionType == OptionTypes.zone
                                  && o.CascadeType == OptionTypes.facility_zone_location && o.CascadeKey == f.FacilityZoneLocation)
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


                DtoSecurityMailConfig securityMail = null;
                if (_settings.EnableSecutyEmail)
                {
                    securityMail = await _db.SecurityMailConfigs.Where(x => x.ZoneId == dtoForm.Zone && x.ZoneFacilityId == dtoForm.ZoneFacility && x.IsActive==true).FirstOrDefaultAsync();
                }
               
                var subject = $"{result.FormDef.Title}";
                var templateName = "WP_Submitted_to_AM";

                var submittedByParts = result.SubmittedBy.Value.Split("|");

                Dictionary<string, string> placeholders = new Dictionary<string, string>
                    {
                        { "WorkPermitName", result.FormDef.Title },
                        { "FacilityName", result.ZoneFacility.Value },
                        { "DateTime", result.SubmittedDate.ToISTString() },
                        { "Requester", submittedByParts[0] },
                        { "Email", submittedByParts[1] },
                        { "Contact", submittedByParts[2] }
                    };
                var toEmail = areaManger.Email;
                var cc = new List<string>();
                if (securityMail != null && !string.IsNullOrEmpty(securityMail.SecurityEmail))
                {
                    cc.Add(securityMail.SecurityEmail);
                }
                
                await _emailService.SendTemplatedEmailAsync(toEmail, subject, templateName, placeholders, cc);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email for {dtoForm.ToJsonString()}");
            }
        }

        private async Task ProcessIncidentEmail(DtoFormSubmissions dtoForm, DtoUser ehsManger, DtoUser areaManger)
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
                                 .Select(u => u.FirstName + " " + u.LastName + "|" + u.Email + "|" + (string.IsNullOrEmpty(u.Mobile) ? "" : u.Mobile))
                                 .FirstOrDefault()
                    },
                    ZoneFacility = new KeyVal
                    {
                        key = f.ZoneFacility,
                        Value = _db.FormOptions
                                 .Where(o => o.OptionKey == f.ZoneFacility && o.OptionType == OptionTypes.zone_facility 
                                 && o.CascadeType == OptionTypes.zone && o.CascadeKey == f.Zone)
                                 .Select(o => o.OptionValue)
                                 .FirstOrDefault()
                    }
                }).FirstOrDefault();

                if (result == null) return;

                var ehsManager = await _db.Users.Where(u => u.Zone == dtoForm.Zone && u.RoleId == (int)Roles.EHSManager && u.ActiveStatus == (int)UserStatus.Active).ToListAsync();

                if (ehsManager == null || ehsManager.Count ==0)
                {
                    _logger.LogError($"EHS manager not found for {dtoForm.ToJsonString()}");
                    return;
                }

                var toEmail = string.Join(",", ehsManager.Select(x => x.Email));
                List<string> ccEmail = null;
                if (areaManger is not null && !string.IsNullOrEmpty(areaManger.Email))
                {
                    ccEmail = new List<string> { areaManger.Email };
                }
                _logger.LogInformation($"INC toEmail {toEmail}, {areaManger.ToJsonString()}");
                var subject = result.FormDef.Title;
                var templateName = "FM_to_EHS_Incident";

                var submittedByParts = result.SubmittedBy.Value.Split("|");
                Dictionary<string, string> placeholders = new Dictionary<string, string>
                    {
                        { "IncidentName", result.FormDef.Title },
                        { "FacilityName", result.ZoneFacility.Value },
                        { "DateTime", result.SubmittedDate.ToISTString() },
                        { "Reporter", submittedByParts[0] },
                        { "Email", submittedByParts[1] },
                        { "Contact", submittedByParts[2] }
                    };

                await _emailService.SendTemplatedEmailAsync(toEmail, subject, templateName, placeholders, ccEmail);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email for {dtoForm.ToJsonString()}");
            }
        }

    }
}
