using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Backend.Data.Transfer;
using Backend.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http.Features;
using System.IO;
using Backend.Config;

namespace Backend.Controllers
{
    [Route("workers")]
    public class WorkerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly DirectoryOptions _directoryOptions;

        public WorkerController(IConfiguration config, ApplicationDbContext context, IMapper mapper, IHostingEnvironment env, DirectoryOptions directoryOptions)
        {
            _context = context;
            _mapper = mapper;
            _directoryOptions = directoryOptions;
        }

        [HttpGet]
        public IEnumerable<WorkerDto> GetAll()
        {
            // Prune worker entries that have not responded in 1h
            _context.RemoveRange(_context.Workers.Where(w => w.ActiveExperimentInstance == null && w.LastRequestTime < DateTime.UtcNow.AddHours(-1)));
            // or did not finish after 48h
            var oldWorker = _context.Workers.Where(w => w.ActiveExperimentInstance != null && w.LastRequestTime < DateTime.UtcNow.AddHours(-48)).
                Include(w => w.ActiveExperimentInstance);
            _context.RemoveRange(oldWorker);
            foreach(var worker in oldWorker)
            {
                worker.ActiveExperimentInstance.Reset();
            }
            _context.RemoveRange();
            _context.SaveChanges();

            return _context.Workers
                .Include(w => w.ActiveExperimentInstance)
                .Select(w => new WorkerDto
                   {
                      Token = w.Token,
                      Capabilities = w.Capabilities,
                      ActiveExperimentInstanceId = w.ActiveExperimentInstance.Id,
                      ActiveExperimentId = w.ActiveExperimentInstance.ExperimentId,
                      ConnectionInfo = w.ConnectionInfo,
                      LastRequestTime = w.LastRequestTime,
                      RegistrationTime = w.RegistrationTime 
                   });
        }

        [HttpGet("{token}", Name = nameof(GetWorkerByToken))]
        public IActionResult GetWorkerByToken(string token)
        {
            var worker = _context.Workers
                .Include(w => w.ActiveExperimentInstance)
                .SingleOrDefault(w => w.Token == token);

            if (worker == null)
            {
                return NotFound();
            }

            return new ObjectResult(worker.MapTo<WorkerDto>(_mapper));
        }

        [HttpPost]
        public IActionResult RegisterSelf([FromBody] WorkerRegistrationDto registrationData)
        {
            var worker = new Worker
            {
                Token = RandomUtils.GetRandomIdentifier(8),
                RegistrationTime = DateTime.UtcNow,
                LastRequestTime = DateTime.UtcNow,
                ActiveExperimentInstance = null,
                Capabilities = registrationData.Capabilities,
                ConnectionInfo = HttpContext.Connection.RemoteIpAddress.ToString()
            };

            _context.Workers.Add(worker);

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    _context.SaveChanges();
                }
                catch (DbUpdateException e)
                {
                    if(i == 4)
                    {
                        return null;
                    }

                    if(e.InnerException != null && e.InnerException.Message == "SQLite Error 5: 'database is locked'.")
                    {
                        Console.WriteLine("Found database locked...");
                        continue;
                    } else
                    {
                        Console.WriteLine("Unexpected exception " + e.Message);
                    }
                }

                break;
            }

            return CreatedAtAction(nameof(GetWorkerByToken), new { token = worker.Token },
                worker.MapTo<WorkerDto>(_mapper));
        }

        [HttpGet("script.py")]
        public IActionResult GetWorkerScript()
        {
            var fs = new FileStream("AppData/WorkerScript/worker.py", FileMode.Open);
            return File(fs, "application/x-python");
        }

        private string getOwnUrl()
        {
            var httpConnectionFeature = HttpContext.Features.Get<IHttpConnectionFeature>();
            return "http://" + httpConnectionFeature?.LocalIpAddress.ToString() + ":" + httpConnectionFeature?.LocalPort.ToString();
        }

        [HttpGet("bootstrap.sh")]
        public IActionResult GetLaunchScript()
        {
            var maxIdleTimeSec = _context.Configuration.SingleOrDefault().MaxIdleTimeSec;

            var launchScript = _directoryOptions.GetFileContents("AppData/CloudInit/bootstrap.sh")
                .Replace("{{Backend}}", getOwnUrl())
                .Replace("{{Capabilities}}", "replaceIfRequired")
                .Replace("{{MaxIdleTime}}", maxIdleTimeSec.ToString());

            return new ContentResult { Content = launchScript };
        }

        [HttpGet("bootstrap_noshutdown.sh")]
        public IActionResult GetLaunchScriptNoShutdown()
        {
            var maxIdleTimeSec = _context.Configuration.SingleOrDefault().MaxIdleTimeSec;

            var launchScript = _directoryOptions.GetFileContents("AppData/CloudInit/bootstrap_noshutdown.sh")
                .Replace("{{Backend}}", getOwnUrl())
                .Replace("{{Capabilities}}", "replaceIfRequired")
                .Replace("{{MaxIdleTime}}", maxIdleTimeSec.ToString());

            return new ContentResult { Content = launchScript };
        }
    }
}
