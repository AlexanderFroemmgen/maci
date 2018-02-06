using System.Collections.Generic;
using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class ParameterDto
    {
        public string Name { get; set; }

        public ParameterType Type { get; set; }

        public ParameterPurpose Purpose { get; set; }

        public string Unit { get; set; }

        public IList<string> Values { get; set; }
    }
}