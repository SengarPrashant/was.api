using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos.Forms
{
    [Table("form_def")]
    public class DtoFormDef
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("title")]
        public string Title { get; set; }
        [Column("desc")]
        public string? Description { get; set; }
        [Column("form_type")]
        public string FormType { get; set; }
        [Column("form_type_key")]
        public string FormKey { get; set; }
    }
}
