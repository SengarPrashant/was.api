using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using was.api.Helpers;
using was.api.Models;
using was.api.Models.Auth;
using was.api.Models.Dtos;
using was.api.Models.Dtos.Forms;
using was.api.Models.Forms;

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
                                               x.field.Required,
                                               x.field.Placeholder,
                                               x.field.Order,
                                               x.field.IsActive,
                                               x.field.OptionType,
                                               SectionId = x.section.Id,
                                               x.field.CascadeField
                                           }).ToList()
                                       }).ToList()
                               }
                           ).FirstOrDefaultAsync();

                // extracting unique option types
                var optionTypes = formDetails?.Sections
                                    .SelectMany(section => section.Fields)
                                    .Select(field => field.OptionType)
                                    .Where(optionType => optionType != null)
                                    .Distinct()
                                    .ToList();
                
                var options = new List<OptionsResponse>();
                if (optionTypes is not null)
                {
                    // fetching all the matching options
                    options = await _db.FormOptions
                                    .Where(x => optionTypes.Contains(x.OptionType))
                                    .Select(x => new OptionsResponse
                                    {
                                        OptionType = x.OptionType,
                                        OptionKey = x.OptionKey,
                                        OptionValue = x.OptionValue,
                                        CascadeType = x.CascadeType,
                                        CascadeKey = x.CascadeKey,
                                        IsActive = x.IsActive
                                    }).ToListAsync();
                }

                return new
                {
                    formDetails,
                    options
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

        public async Task<bool> SubmitForms(FormSubmissionRequest request, CurrentUser user)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var formDto = new DtoFormSubmissions
                {
                    FormId = request.FormId,
                    FormData = request.FormData,
                    Status = request.Status,
                    SubmittedBy = user.Id,
                    SubmittedDate = DateTime.UtcNow
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
                        FileName = file.Name,
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
    }
}
