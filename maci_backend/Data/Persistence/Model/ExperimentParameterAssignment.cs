using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Data.Persistence.Model
{
    /// <summary>
    ///     This is a glue class for establishing a many-to-many relationship between
    ///     ExperimentInstance and ParameterInstances. As of EF Core 1.0.0, many-to-many
    ///     relationships are not implicitly supported.
    /// </summary>
    public class ExperimentParameterAssignment : Entity
    {
        public int ExperimentInstanceId { get; set; }

        [ForeignKey(nameof(ExperimentInstanceId))]
        public ExperimentInstance ExperimentInstance { get; set; }

        public int ParameterValueId { get; set; }

        [ForeignKey(nameof(ParameterValueId))]
        public ParameterValue ParameterValue { get; set; }

        public override string ToString()
        {
            return ParameterValue.ToString();
        }
    }
}