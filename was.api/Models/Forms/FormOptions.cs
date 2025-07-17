using System.ComponentModel.DataAnnotations.Schema;

namespace was.api.Models.Forms
{
    public class OptionsRequest
    {
        public required string OptionType { get; set; }
        public string? CascadeType { get; set; }
        public string? CascadeKey { get; set; }
    }
    public class OptionsResponse
    {
       // public int Id { get; set; }
        public string OptionType { get; set; }
        public string OptionKey { get; set; }
        public string OptionValue { get; set; }
        public string? CascadeKey { get; set; }
        public string? CascadeType { get; set; }
        public bool IsActive { get; set; }
    }
}

