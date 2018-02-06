using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Backend.Data.Persistence.Model
{
    public class Parameter : Entity
    {
        public int ExperimentId { get; set; }

        [ForeignKey(nameof(ExperimentId))]
        public Experiment Experiment { get; set; }

        /// <summary>
        ///     Name of the parameter as it is used within the experiment script.
        /// </summary>
        public string Name { get; set; }

        public ParameterType Type { get; set; }

        public ParameterPurpose Purpose { get; set; }

        public string Unit { get; set; }

        /// <summary>
        ///     List of all values that can be used for this specific parameter.
        /// </summary>
        public ICollection<ParameterValue> Values { get; set; }

        public override string ToString()
        {
            return $"{Name} : [{string.Join(", ", Values.Select(i => i.Value))}]";
        }
    }

    public enum ParameterType
    {
        String,
        Int,
        Float
    }

    public enum ParameterPurpose
    {
        Configuration,
        Environment
    }
}