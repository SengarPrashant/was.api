using System.Text.Json;

namespace was.api.Models.Forms
{
    public class FormSubmissionRequest
    {
        public long FormId { get; set; }
        public JsonElement FormData { get; set; }
        public string? Remarks { get; set; }
        public IList<IFormFile> Files { get; set; } = [];
        public string Status { get; set; } = "1";
    }
}
