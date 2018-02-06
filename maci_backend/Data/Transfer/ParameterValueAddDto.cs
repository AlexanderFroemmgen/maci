using System.ComponentModel.DataAnnotations;

namespace Backend.Data.Transfer
{
    public class ParameterValueAddDto
    {
        [Required]
        public string ParameterName { get; set; }

        [Required]
        public string Value { get; set; }
    }
}