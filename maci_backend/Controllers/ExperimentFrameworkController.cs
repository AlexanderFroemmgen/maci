using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Backend.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Backend.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Controllers
{
    [Route("framework")]
    public class ExperimentFrameworkController : Controller
    {
        private readonly DirectoryOptions _directoyOptions;
        private readonly ILogger<ExperimentFrameworkController> _logger;
        private readonly GitRemoteOptions _gitRemoteOptions;

        private readonly string globalpath;
        private readonly string projectpath;
        private readonly string datalocation;

        public ExperimentFrameworkController(DirectoryOptions directoyOptions, ILogger<ExperimentFrameworkController> logger,
            IOptions<GitRemoteOptions> gitRemoteOptions)
        {
            _directoyOptions = directoyOptions;
            _logger = logger;
            _gitRemoteOptions = gitRemoteOptions.Value;

            globalpath = directoyOptions.DataLocation + "/ExperimentFramework";
            projectpath = directoyOptions.DataLocation + "/ExperimentTemplates";
            datalocation = directoyOptions.DataLocation;
        }

        [HttpGet("datalocation")]
        public ActionResult GetDataLocation()
        {
            return new ObjectResult(new
            {
                DataLocation = datalocation
            });
        }

        public IEnumerable<string> GetAllFiles()
        {
            return _directoyOptions.GetAllFilesRecursively(globalpath);
        }

        private void ensureDirectoryExists(string path)
        {
            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                di.Create();
            }
        }

        [HttpGet("{experimentName}")]
        public IEnumerable<string> GetAllFiles(string experimentName)
        {
            var path = projectpath + "/" + experimentName + "/framework";

            ensureDirectoryExists(path);
            return _directoyOptions.GetAllFilesRecursively(path);
        }

        [HttpPost("{experimentName}")]
        public IActionResult AddFiles(string experimentName) {
            _logger.LogInformation("handling Framework.AddFiles for sim="+experimentName);

            string filenames = "";
            foreach (var file in Request.Form.Files)
            {
                _logger.LogInformation("Handling file "+file.FileName);
                var destFilespec = Path.Combine(projectpath + "/" + experimentName + "/framework/" +
                                                Path.GetFileName(file.FileName));
                if (!destFilespec.StartsWith(projectpath)) throw new ArgumentException("Invalid file name!");
                _logger.LogInformation("Writing to: "+destFilespec);
                using (var fs = new FileStream(destFilespec, FileMode.Create, FileAccess.Write))
                {
                    file.CopyTo(fs);
                }
                filenames += ", " + file.FileName;
            }
            GitIntegration.CreateBackup(projectpath + "/" + experimentName, 
                "updated framework folder"+filenames, _gitRemoteOptions);
            return Ok();
        }

    }
}
