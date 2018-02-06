using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.WorkerHost
{
    public class ScalingService
    {
        private readonly IHostingEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly WorkerHostService _hostService;

        public ScalingService(IHostingEnvironment env, ApplicationDbContext context, WorkerHostService hostService)
        {
            _env = env;
            _context = context;
            _hostService = hostService;
        }
        
        private bool capabilitiesMatch(IEnumerable<string> image, IEnumerable<string> experiment)
        {
            return !experiment.Except(image).Any();
        }

        public void Scale(Experiment experiment)
        {
            // TODO: fix: the experiment we get here as no experiment list loaded,
            // we need some performance and trigger considerations...
            // we actually want to check for the overall workload of the system, not just a single experiment
            // we might add different regions
         


            var timeIntervalBetweenScalingInSeconds = 60 * 5; // this might compensate boot times
            var maxNumberOfNewWorkersPerScale = 10;
            var maxNumberOfWorkerAlreadyRunning = 50;

            var pendingExperimentInstances = experiment.ExperimentInstances.Where(si => si.Status == ExperimentStatus.Pending).Count();
            var runningExperimentInstances = experiment.ExperimentInstances.Where(si => si.Status == ExperimentStatus.Running).Count();

            /*
            var expectedBootTime_s = 60;
            var finishedExperiments = experiment.ExperimentInstances.Where(si => si.Status == ExperimentStatus.Finished);
            if (finishedExperiments.Count() > 0) {
                var averageExperimentTime_s = finishedExperiments.Average(si => si.WorkFinished.Subtract(si.WorkStarted).TotalSeconds);
            }
            */

	        if (runningExperimentInstances > maxNumberOfWorkerAlreadyRunning) {
		        return;
	        }

            /* "running" is a rough estimator of number of workers */
            if (pendingExperimentInstances > runningExperimentInstances * 10 && pendingExperimentInstances > 50)
            {
                /* check for potential images */
                var imagesTmp = _hostService.GetAvailableImagesAsync().Result;
                var images = imagesTmp.Where(i => capabilitiesMatch(i.Capabilities, experiment.RequiredCapabilities));

                foreach (var image in images)
                {
                    /* is auto scaling active? */
                    var scalingGroup = _context.ScalingGroups.Where(sg => sg.Active && sg.ImageId == image.Id).SingleOrDefault();
                    if (scalingGroup == null)
                        continue;

                    if (DateTime.Now.Subtract(scalingGroup.LastScalingTime).TotalSeconds < timeIntervalBetweenScalingInSeconds)
                        continue;

                    scalingGroup.LastScalingTime = DateTime.Now;
                    _context.SaveChanges();

                    var numberOfNewWorkers = Math.Min(pendingExperimentInstances / 10, maxNumberOfNewWorkersPerScale);
                    var maxIdleTimeSec = _context.Configuration.SingleOrDefault().MaxIdleTimeSec;

                    try
                    {
	                    _hostService.LaunchInstanceAsync(image.HostId, image.Id, numberOfNewWorkers, maxIdleTimeSec).Wait();
			        }
			        catch(Exception e)
			        {
			            Console.WriteLine("Exception while starting new instances in autoscale.\n" + e.Message);
			            return;
                    }

                    /* we found one, stop here */
                    return;
                }
            }
        }
    }
}
