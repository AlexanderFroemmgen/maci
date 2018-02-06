using System.Threading.Tasks;
using Backend.Util;
using Backend.WorkerHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Backend.Data.Persistence;
using System;
using Backend.Data.Transfer;

namespace Backend.Controllers
{
    [Route("workerhosts")]
    public class WorkerHostController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHostingEnvironment _env;
        private readonly WorkerHostService _hostService;

        public WorkerHostController(ApplicationDbContext context, IHostingEnvironment env, WorkerHostService hostService)
        {
            _context = context;
            _env = env;
            _hostService = hostService;
        }

        private bool isAutoscaling(ImageDto image)
        {
            var scalingGroup = _context.ScalingGroups.Where(sg => sg.HostId == image.HostId && sg.ImageId == image.Id).FirstOrDefault();
            return scalingGroup != null ? scalingGroup.Active : false;
        }

        [HttpGet("images")]
        public async Task<IActionResult> GetImages()
        {
            try
            {
                var images = await _hostService.GetAvailableImagesAsync();
                
                return new ObjectResult(images.Select(i => new ImageDto
                {
                    Id = i.Id,
                    HostId = i.HostId,
                    Name = i.Name,
                    Capabilities = i.Capabilities,
                    Description = i.Description,
                    AutoScale = isAutoscaling(i)
                }));
            }
            catch (WorkerHostException e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost("images/{imageId}/active/{value}")]
        public async Task<IActionResult> SetAutoScaling(string imageId, int value)
        {
            var scalingGroup = _context.ScalingGroups.Where(sg => sg.ImageId == imageId).SingleOrDefault();
            if(scalingGroup == null)
            {
                /* not existing in database yet */
                var images = await _hostService.GetAvailableImagesAsync();
                var image = images.Where(i => i.Id == imageId).SingleOrDefault();
                if(image == null)
                {
                    return NotFound();
                }

                scalingGroup = new Data.Persistence.Model.ScalingGroup
                {
                    ImageId = imageId,
                    HostId = image.HostId,
                    Active = value == 1,
                    LastScalingTime = DateTime.MinValue
                };
                _context.ScalingGroups.Add(scalingGroup);
            }
            else
            {
                scalingGroup.Active = value == 1;
            }
            _context.SaveChanges();

            return Ok();
        }


        [HttpGet("instances")]
        public async Task<IActionResult> GetInstances()
        {
            try
            {
                var instances = await _hostService.GetRunningInstancesAsync();
                return new ObjectResult(instances);
            }
            catch (WorkerHostException e)
            {
                return StatusCode(500, e.Message);
            }
        }

        [HttpPost("instances")]
        public async Task<IActionResult> LaunchInstance([FromBody] LaunchInstanceDto launchData)
        {
            var maxIdleTimeSec = _context.Configuration.SingleOrDefault().MaxIdleTimeSec;

            try
            {
                await _hostService.LaunchInstanceAsync(launchData.HostId, launchData.ImageId, 1, maxIdleTimeSec);
            }
            catch (WorkerHostException e)
            {
                return StatusCode(500, e.Message);
            }

            return NoContent();
        }

        [HttpDelete("{hostId}/instances/{instanceId}")]
        public async Task<IActionResult> TerminateInstance(string hostId, string instanceId)
        {
            try
            {
                await _hostService.TerminateInstanceAsync(hostId, instanceId);
            }
            catch (WorkerHostException e)
            {
                return StatusCode(500, e.Message);
            }

            return NoContent();
        }

        [HttpPost("timeout")]
        public IActionResult SetTimeout([FromBody] TimeoutDto timeoutDto)
        {
            var config = _context.Configuration.SingleOrDefault(); 
            config.MaxIdleTimeSec = timeoutDto.Timeout;
            _context.SaveChanges();

            return Ok();
        }

        [HttpGet("timeout")]
        public IActionResult GetTimeout()
        {
            var config = _context.Configuration.SingleOrDefault();

            return new ObjectResult(new TimeoutDto
            {
                Timeout = config.MaxIdleTimeSec
            });
        }
    }

    public class LaunchInstanceDto
    {
        public string HostId { get; set; }
        public string ImageId { get; set; }
    }
}