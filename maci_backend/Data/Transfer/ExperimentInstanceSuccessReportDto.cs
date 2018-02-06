using System.Collections.Generic;

namespace Backend.Data.Transfer
{
    public class ExperimentInstanceSuccessReportDto
    {
        public string Log { get; set; }
        public IEnumerable<RecordDto> Records { get; set; }
        public IEnumerable<LogMessageDto> LogMessages { get; set; }
    }
}