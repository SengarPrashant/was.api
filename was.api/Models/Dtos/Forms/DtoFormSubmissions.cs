using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_submissions")]
    public class DtoFormSubmissions
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("form_id")]
        public long FormId { get; set; }

        [Column("submitted_by")]
        public int SubmittedBy { get; set; }

        [Column("submitted_date")]
        public DateTime SubmittedDate { get; set; }

        [Column("form_data")]
        public JsonElement FormData { get; set; }

        [Column("status")]
        public string Status { get; set; }
        [Column("pending_with")]
        public int PendingWith { get; set; }

        [Column("facility_zone_location")]
        public string FacilityZoneLocation { get; set; }
        [Column("zone")]
        public string Zone { get; set; }
        [Column("zone_facility")]
        public string ZoneFacility { get; set; }

        [Column("updated_by")]
        public int UpdatedBy { get; set; }

        [Column("updated_date")]
        public DateTime? UpdatedDate { get; set; }
        [Column("project")]
        public string? Project { get; set; }
        
    }
    [Table("form_documents")]
    public class DtoFormDocument
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("form_submission_id")]
        public long FormSubmissionId { get; set; }
        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;
        [Column("content")]
        public byte[] Content { get; set; } = [];
        [Column("content_type")]
        public string ContentType { get; set; } = string.Empty;

    }

    [Table("form_workflow_history")]
    public class DtoFormWorkFlowHistory
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("form_submission_id")]
        public long FormSubmissionId { get; set; }

        [Column("status")]
        public string Status { get; set; }
        [Column("action_by")]
        public int ActionBy { get; set; }
        [Column("action_date")]
        public DateTime ActionDate { get; set; }
        [Column("remarks")]
        public string Remarks { get; set; }
    }

    public class DtoFormSubmissionResult
    {
        [Column("id")]
        public long Id { get; set; }
        [Column("form_id")]
        public long FormId { get; set; }

        [Column("submitted_by")]
        public int SubmittedBy { get; set; }

        [Column("submitted_date")]
        public DateTime SubmittedDate { get; set; }

        [Column("form_data")]
        public JsonElement? FormData { get; set; }
        [Column("short_desc")]
        public string? ShortDesc { get; set; }
        [Column("status")]
        public string Status { get; set; }
        [Column("pending_with")]
        public int PendingWith { get; set; }
        [Column("facility_zone_location")]
        public string FacilityZoneLocation { get; set; }
        [Column("zone")]
        public string Zone { get; set; }
        [Column("zone_facility")]
        public string ZoneFacility { get; set; }
        [Column("project")]
        public string? Project { get; set; }
        [Column("title")]
        public string Title { get; set; }
        [Column("desc")]
        public string? Description { get; set; }
        [Column("form_type")]
        public string FormType { get; set; }
        [Column("form_type_key")]
        public string FormTypeKey { get; set; }
    }


    [Table("security_email_config")]
    public class DtoSecurityMailConfig
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("zone_id")]
        public string ZoneId { get; set; }
        [Column("zone_facility_id")]
        public string ZoneFacilityId { get; set; }
        [Column("security_email")]
        public string SecurityEmail { get; set; }
    }
}
