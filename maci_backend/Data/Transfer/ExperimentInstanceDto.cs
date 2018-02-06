using System.Collections.Generic;
using Backend.Data.Persistence.Model;
using System;

namespace Backend.Data.Transfer
{
    public class ExperimentInstanceDto
    {
        public int ExperimentId { get; set; }

        public int Id { get; set; }

        public IDictionary<string, object> Configuration { get; set; }

        public ExperimentStatus Status { get; set; }

        public string AssignedServer { get; set; }

        public DateTime WorkStarted { get; set; }

        public string Log { get; set; }

        public IEnumerable<LogMessageDto> LogMessages { get; set; }

        public bool HasWarnings { get; set; }
    }
}