using System.Threading.Tasks;
using Backend.Util;
using Backend.WorkerHost;
using Microsoft.AspNetCore.Hosting;
using Backend.Data.Persistence;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Backend.Data.Persistence.Model;

namespace Backend.Controllers
{
    [Route("workerscaling")]
    public class WorkerScalingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHostingEnvironment _env;
        private readonly WorkerHostService _hostService;

        public WorkerScalingController(ApplicationDbContext context, IHostingEnvironment env, WorkerHostService hostService)
        {
            _context = context;
            _env = env;
            _hostService = hostService;
        }

        [HttpGet]
        public IEnumerable<ScalingGroupDto> GetScalingGroups()
        {
            var result = _context.ScalingGroups.Select(sg => new ScalingGroupDto
            {
                Id = sg.Id,
                HostId = sg.HostId,
                ImageId = sg.ImageId,
                Active = sg.Active
            });
            return result;
        }

        [HttpPost]
        public IActionResult AddScalingGroup([FromBody] ScalingGroupDto scalingGroupData)
        {
            _context.ScalingGroups.Add(new Data.Persistence.Model.ScalingGroup
            {
                HostId = scalingGroupData.HostId,
                ImageId = scalingGroupData.ImageId,
                Active = false
            });
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost("{groupId}/active/{value}")]
        public IActionResult SetActive(int groupId, int value)
        {
            _context.ScalingGroups.Where(sg => sg.Id == groupId).Single().Active = value == 1;
            _context.SaveChanges();

            return Ok();
        }

        [HttpDelete("{groupId}")]
        public IActionResult DeleteGroup(int groupId)
        {
            _context.ScalingGroups.RemoveRange(_context.ScalingGroups.Where(sg => sg.Id == groupId));
            _context.SaveChanges();

            return Ok();
        }
    }

    public class ScalingGroupDto
    {
        public int Id { get; set; }
        public string HostId { get; set; }
        public string ImageId { get; set; }
        public bool Active { get; set; }
    }
}