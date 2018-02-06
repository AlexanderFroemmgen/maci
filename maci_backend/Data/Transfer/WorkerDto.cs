using System;
using System.Collections.Generic;

namespace Backend.Data.Transfer
{
    public class WorkerDto
    {
        public string Token { get; set; }

        public DateTime RegistrationTime { get; set; }

        public DateTime LastRequestTime { get; set; }

        public string ConnectionInfo { get; set; }

        public int? ActiveExperimentInstanceId { get; set; }

        public int? ActiveExperimentId { get; set; }

        public IEnumerable<string> Capabilities { get; set; }
    }
}
