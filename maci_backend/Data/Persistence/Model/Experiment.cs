using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Backend.Data.Persistence.Model
{
    public class Experiment : Entity
    {
        private string RequiredCapabilitiesSerialized { get; set; }

        public DateTime Created { get; set; }

        [Required]
        public string Script { get; set; }

        public string ScriptInstall { get; set; }
        
        public int Repetitions { get; set; }

        /// <summary>
        ///     List of parameters that are simulated. Parameter names must be unique.
        /// </summary>
        public ICollection<Parameter> Parameters { get; set; }

        public ICollection<ExperimentInstance> ExperimentInstances { get; set; }

        [NotMapped]
        public IEnumerable<string> RequiredCapabilities
        {
            get { return RequiredCapabilitiesSerialized?.Split(',') ?? Enumerable.Empty<string>(); ; }
            set { RequiredCapabilitiesSerialized = value == null || !value.Any() ? null : string.Join(",", value); }
        }

        public string Language { get; set; }

        public string PermutationFilter { get; set; }

        public string RunName { get; set; }

        public string FileName { get; set; }

        public int Timeout { get; set; }
    }
}