using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Data.Transfer
{
    public class ExperimentHistoryDto
    {
        public class ExperimentHistoryItemDto
        {
            public string CommitId;
            public string Author;
            public string Date;
            public string Message;
            public string MergeId;

        }
        public string RepoRemoteUrl;
        public IList<ExperimentHistoryItemDto> History { get; set; }
    }
}
