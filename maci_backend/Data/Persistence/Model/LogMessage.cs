using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Data.Persistence.Model
{
    public class LogMessage : Entity
    {
        public string Key { get; set; }

        public LogMessageType Type { get; set; }

        public int Offset { get; set; }

        public string Message { get; set; }

        public int ExperimentInstanceId { get; set; }

        [ForeignKey(nameof(ExperimentInstanceId))]
        public ExperimentInstance ExperimentInstance { get; set; }
    }

    public enum LogMessageType
    {
        Info,
        Warning
    }
}