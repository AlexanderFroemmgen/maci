using System;
using System.Collections.Generic;
using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class ExperimentFileDto
    {
        public string Name { get; set; }

        public string Script { get; set; }

        public string ScriptInstall { get; set; }

        public IDictionary<string, string> Configurations { get; set; }
    }
}