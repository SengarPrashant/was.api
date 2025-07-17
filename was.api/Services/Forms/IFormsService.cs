using was.api.Models.Forms;

namespace was.api.Services.Forms
{
    public interface IFormsService
    {
        Task<object?> GetFormDetails(string formType, string key);
        Task<List<OptionsResponse>> GetOptions(OptionsRequest request);
    }
}
