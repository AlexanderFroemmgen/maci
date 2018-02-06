using System.ComponentModel.DataAnnotations;

namespace Backend.Data.Persistence.Model
{
    public class Entity
    {
        [Key]
        public int Id { get; set; }
    }
}