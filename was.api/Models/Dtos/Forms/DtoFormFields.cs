using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_fields")]
    public class DtoFormFields
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("form_id")]
        public int FormId { get; set; }
        [Column("field_key")]
        public string FieldKey { get; set; }
        [Column("type")]
        public string Type { get; set; }
        [Column("label")]
        public string Label { get; set; }
        [Column("placeholder")]
        public string? Placeholder { get; set; }
        [Column("required")]
        public bool Required { get; set; }
        [Column("order")]
        public int Order { get; set; }
        [Column("option_type")]
        public string OptionType { get; set; }
        [Column("cascade_field")]
        public string? CascadeField { get; set; }
        [Column("section_id")]
        public int SectionId { get; set; }
        [Column("is_active")]
        public bool IsActive { get; set; }
        [Column("col_span")]
        public int ColSpan { get; set; }
       
    }
}
