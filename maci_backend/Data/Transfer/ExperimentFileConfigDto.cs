using System;
using System.Collections.Generic;
using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class ExperimentFileConfigDto
    {
        public string Name { get; set; }

        public string Configuration { get; set; }
    }
}