using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Backend.Data.Persistence.Model
{
    public class Worker
    {
        private string CapabilitiesSerialized { get; set; }

        [Key]
        [Required]
        public string Token { get; set; }

        public DateTime RegistrationTime { get; set; }

        public DateTime LastRequestTime { get; set; }

        public string ConnectionInfo { get; set; }

        public ExperimentInstance ActiveExperimentInstance { get; set; }

        [NotMapped]
        public IEnumerable<string> Capabilities
        {
            get { return CapabilitiesSerialized?.Split(',') ?? Enumerable.Empty<string>(); }
            set { CapabilitiesSerialized = value == null || !value.Any() ? null : string.Join(",", value); }
        }
    }
}