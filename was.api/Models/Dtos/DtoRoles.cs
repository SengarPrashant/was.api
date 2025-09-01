using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Dtos
{
    [Table("role_master")]
    public class DtoRoles
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }
        [Column("key_name")]
        public string KeyName { get; set; }
        [Column("is_active")]
        public bool IsActive { get; set; }
    }
}
