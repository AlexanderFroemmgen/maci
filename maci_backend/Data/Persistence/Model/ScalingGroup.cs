using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Backend.Data.Persistence.Model
{
    public class ScalingGroup : Entity
    {
        [Required]
        public string HostId { get; set; }

        [Required]
        public string ImageId { get; set; }

        [Required]
        public bool Active { get; set; }

        public DateTime LastScalingTime { get; set; }
    }
}