using System.Collections.Generic;

namespace Backend.Data.Transfer
{
    public class ExperimentCreateDto
    {
        public string Script { get; set; }

        public string ScriptInstall { get; set; }

        public IList<ParameterDto> Parameters { get; set; }

        public IList<string> RequiredCapabilities { get; set; }

        public string Language { get; set; }

        public string PermutationFilter { get; set; }

        public int Repetitions { get; set; }

        public int Seeds { get; set; }

        public string RunName { get; set; }

        public string FileName { get; set; }

        public bool TestRun { get; set; }
    }
}