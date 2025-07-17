using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_submissions")]
    public class DtoFormSubmissions
    {
        [Column("id")]
        public Int64 Id { get; set; }
        [Column("form_id")]
        public int FormId { get; set; }

        [Column("submitted_by")]
        public string SubmittedBy { get; set; }

        [Column("submitted_date")]
        public DateTime SubmittedDate { get; set; }

        [Column("form_data")]
        public string FormData { get; set; }

        [Column("status")]
        public int Status { get; set; }
    }
}
