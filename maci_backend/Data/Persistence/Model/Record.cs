using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Data.Persistence.Model
{
    public class Record : Entity
    {
        public int ExperimentInstanceId { get; set; }

        [ForeignKey(nameof(ExperimentInstanceId))]
        public ExperimentInstance ExperimentInstance { get; set; }

        public string Key { get; set; }
        
        public double Offset { get; set; }

        public string Value { get; set; }

        public string Key2 { get; set; }

        public string Key3 { get; set; }
    }
}