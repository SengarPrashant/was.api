using System.Text.Json;

namespace was.api.Models.Forms
{
    public class FormSubmissionRequest
    {
        public long? Id { get; set; } = 0;
        public long FormId { get; set; } = 0;
        public string FormType { get; set; }
        public string FormData { get; set; }
        public string? Remarks { get; set; } = string.Empty;
        public IList<IFormFile> Files { get; set; } = [];
        public string Status { get; set; } = "1";
        public string FacilityZoneLocation { get; set; }
        public string Zone { get; set; }
        public string ZoneFacility { get; set; }
        public string? Project { get; set; }
    }

    public class GetFormRequest
    {
        public string? FormType { get; set; }
        public string? FormTypeId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
    public class FormSubmissionDetail : FormResponse
    {
        public List<FormDocument> Documents { get; set; }
        public List<FormWfResponse> Workflow { get; set; }
    }

    public class FormDocument
    {
        public long Id { get; set; }
        public long FormSubmissionId { get; set; }
        public string FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[]? Content { get; set; }
    }

    public class FormWfResponse
    {
        public string ActionBy { get; set; }
        public string ActionDate { get; set; }
        public string Action { get; set; }
        public string Remarks { get; set; }
    }

    public class StatusCount 
    {
        public int Count { get; set; }
        public string FormType { get; set; }
        public string Status { get; set; }
    }
    public class FormResponse
    {
        public string? RequestId { get; set; }
        public long Id { get; set; }
        public long FormId { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string? ShortDesc { get; set; }
        public JsonElement? FormData { get; set; }
        public KeyVal Status { get; set; }
        public KeyVal PendingWith { get; set; }
        public KeyVal SubmittedBy { get; set; }
        public KeyVal FacilityZoneLocation { get; set; }
        public KeyVal Zone { get; set; }
        public KeyVal ZoneFacility { get; set; }
        public string? Project { get; set; }
        public int DocumentCount { get; set; }
        public string FormTitle { get; set; }
        public string? FormDes { get; set; }
        public string FormType { get; set; }
        public string FormTypeKey { get; set; }
    }
    public class KeyVal
    {
        public string key { get; set; }
        public string Value { get; set; }
    }
   
    public class FormStatusUpdateRequest
    {
        public long Id { get; set; }
        public string Status { get; set; }
        public string? Remarks { get; set; }
    }
}
