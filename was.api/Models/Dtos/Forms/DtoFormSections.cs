using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_sections")]
    public class DtoFormSections
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("title")]
        public string Title { get; set; }
        [Column("desc")]
        public string? Description { get; set; }
        [Column("section_style")]
        public string? SectionStyle { get; set; }
        [Column("order")]
        public int Order { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}
