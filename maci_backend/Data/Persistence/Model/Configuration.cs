using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Data.Persistence.Model
{
    public class Configuration: Entity
    {
        public int MaxIdleTimeSec { get; set; }
    }
}
