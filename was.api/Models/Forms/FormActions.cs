using System.Text.Json;

namespace was.api.Models.Forms
{
    public class FormSubmissionRequest
    {
        public long FormId { get; set; } = 0;
        public string FormData { get; set; }
        public string? Remarks { get; set; } = string.Empty;
        public IList<IFormFile> Files { get; set; } = [];
        public string Status { get; set; } = "1";
        public string FacilityZoneLocation { get; set; }
        public string Zone { get; set; }
        public string ZoneFacility { get; set; }
    }

    public class GetFormRequest
    {
        public string FormType { get; set; }
        public string FormTypeId { get; set; }
    }
    public class FormResponse
    {
        public long Id { get; set; }
        public long FormId { get; set; }
        public DateTime SubmittedDate { get; set; }
        public JsonElement FormData { get; set; }
        public KeyVal Status { get; set; }
        public KeyVal SubmittedBy { get; set; }
        public KeyVal FacilityZoneLocation { get; set; }
        public KeyVal Zone { get; set; }
        public KeyVal ZoneFacility { get; set; }
        public int DocumentCount { get; set; }

    }
    public class KeyVal
    {
        public string key { get; set; }
        public string Value { get; set; }
    }
   
}
