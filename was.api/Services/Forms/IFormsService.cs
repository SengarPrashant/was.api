using was.api.Models.Auth;
using was.api.Models.Dtos;
using was.api.Models.Forms;

namespace was.api.Services.Forms
{
    public interface IFormsService
    {
        Task<object?> GetFormDetails(string formType, string key);
        Task<List<OptionsResponse>> GetOptions(OptionsRequest request);
        Task<IEnumerable<DtoRoles>> GetRoles();
        Task<bool> SubmitForms(FormSubmissionRequest request, CurrentUser user);
    }
}
