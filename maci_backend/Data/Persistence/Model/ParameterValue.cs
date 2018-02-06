using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Data.Persistence.Model
{
    public class ParameterValue : Entity
    {
        public int ParameterId { get; set; }

        [ForeignKey(nameof(ParameterId))]
        public Parameter Parameter { get; set; }

        public string Value { get; set; }

        /// <summary>
        ///     List of experiment instances this parameter instance is used in.
        /// </summary>
        public ICollection<ExperimentParameterAssignment> ExperimentInstances { get; set; }

        public override string ToString()
        {
            return $"{Parameter.Name}={Value}";
        }
    }
}