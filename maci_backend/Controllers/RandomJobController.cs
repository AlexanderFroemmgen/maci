using System;
using System.Linq;
using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace Backend.Controllers
{
    [Route("random_job")]
    public class RandomJobController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RandomJobController(ApplicationDbContext context)
        {
            _context = context;
        }

        private static ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        [HttpGet]
        public IActionResult RandomJob([FromHeader(Name = "Worker-Token")] string token)
        {
            /* Here, we basically follow the pattern in https://msdn.microsoft.com/de-de/library/system.threading.readerwriterlock(v=vs.110).aspx */
            
            _lock.EnterWriteLock();

            try
            {
                var worker = _context.Workers.SingleOrDefault(w => w.Token == token);
                if (worker == null)
                {
                    return StatusCode(403, "This worker is not registered.");
                }

                worker.LastRequestTime = DateTime.UtcNow;

                // Find all experiments that the worker can actually run.
                var experiments =
                    _context.Experiments.Where(s => !s.RequiredCapabilities.Except(worker.Capabilities).Any())
                        .Select(s => s.Id);

                // For now, rather than a random instance it just takes the first in the list.
                var instance =
                    _context.ExperimentInstances
                        .Include(i => i.Experiment)
                        .Where(i => i.Status == ExperimentStatus.Pending && experiments.Contains(i.ExperimentId))
                        .OrderByDescending(i => i.Priority)
                        .FirstOrDefault();

                if (instance == null)
                {
                    _context.SaveChanges();
                    return NotFound("No outstanding experiment.");
                }

                instance.AssignedWorkerToken = worker.Token;
                instance.Status = ExperimentStatus.Running;
                instance.WorkStarted = DateTime.UtcNow;

                /* reset all experiments which are assigned by this worker */
                foreach (var si in _context.ExperimentInstances.Where(si => si.AssignedWorkerToken == worker.Token))
                {
                    si.Reset();
                }

                _context.SaveChanges();

                return RedirectToRoute("ExperimentInstance", new { simid = instance.Experiment.Id, instanceid = instance.Id });
            }
            finally {
                _lock.ExitWriteLock();
            }
        }
    }
}