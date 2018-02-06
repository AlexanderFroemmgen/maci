using System;
using System.Collections.Generic;
using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class ExperimentDto
    {
        public int Id { get; set; }

        public DateTime Created { get; set; }

        public string Script { get; set; }

        public int Repetitions { get; set; }

        public ExperimentStatus Status { get; set; }

        public List<ParameterDto> Parameters { get; set; }

        public IEnumerable<string> RequiredCapabilities { get; set; }

        public string RunName { get; set; }

        public string FileName { get; set; }

        public int Timeout { get; set; }

        public string PermutationFilter { get; set; }
    }
}