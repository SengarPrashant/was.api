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
        Task<bool> SubmitForm(FormSubmissionRequest request, CurrentUser user);
        Task<List<OptionsResponse>> GetAllOptions();
        public Task<List<FormResponse>> GetInbox(GetFormRequest request, CurrentUser user);
        public Task<bool> SubmisstionAllowed(string formType, string key, CurrentUser user);
    }
}
