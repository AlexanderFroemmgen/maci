using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Backend.Data.Persistence.Model
{
    public class ExperimentInstance : Entity
    {
        public int ExperimentId { get; set; }

        [ForeignKey(nameof(ExperimentId))]
        public Experiment Experiment { get; set; }

        public ExperimentStatus Status { get; set; }

        public int Priority { get; set; }

        public string Log { get; set; }

        /// <summary>
        ///     List of parameter values this experiment instance uses.
        /// </summary>
        public ICollection<ExperimentParameterAssignment> ParameterValues { get; set; }

        [ConcurrencyCheck]
        public string AssignedWorkerToken { get; set; }

        public Worker AssignedWorker { get; set; }

        public DateTime WorkStarted { get; set; }

        public DateTime WorkFinished { get; set; }

        /// <summary>
        ///     List of measurements that were made during experiment.
        /// </summary>
        public ICollection<Record> Records { get; set; }

        public ICollection<LogMessage> LogMessages { get; set; }

        public override string ToString()
        {
            return string.Join(", ", ParameterValues.Select(p => p.ParameterValue.ToString()));
        }

        public void Reset()
        {
            Status = ExperimentStatus.Pending;
            AssignedWorker = null;
            AssignedWorkerToken = null;
            Log = "";
            Records = null;
            LogMessages = null;
        }
    }
}