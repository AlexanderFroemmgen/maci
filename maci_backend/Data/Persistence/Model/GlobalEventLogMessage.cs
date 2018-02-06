using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Data.Persistence.Model
{
    public class GlobalEventLogMessage : Entity
    {
        public GlobalEventLogMessageType Type { get; set; }

        public DateTime Time { get; set; }

        public string Message { get; set; }

        public int ExperimentId { get; set; }

        public int ExperimentInstanceId { get; set; }
    }

    public enum GlobalEventLogMessageType
    {
        Info,
        Warning,
        Error
    }
}