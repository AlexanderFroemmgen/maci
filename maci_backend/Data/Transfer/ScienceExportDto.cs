using System;
using System.Collections.Generic;
using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class ScienceExportDto
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int[] Experiments { get; set; }
    }
}