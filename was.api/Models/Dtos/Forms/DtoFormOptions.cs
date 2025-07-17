using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_options")]
    public class DtoFormOptions
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("option_type")]
        public string OptionType { get; set; }
        [Column("option_key")]
        public string OptionKey { get; set; }
        [Column("option_value")]
        public string OptionValue { get; set; }
        [Column("cascade_key")]
        public string? CascadeKey { get; set; }
        [Column("cascade_type")]
        public string? CascadeType { get; set; }
        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}
