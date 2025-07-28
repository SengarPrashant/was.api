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
        [Column("facility_zone_location")]
        public string FacilityZoneLocation { get; set; }
        [Column("zone")]
        public string Zone { get; set; }
        [Column("zone_facility")]
        public string ZoneFacility { get; set; }
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
}
