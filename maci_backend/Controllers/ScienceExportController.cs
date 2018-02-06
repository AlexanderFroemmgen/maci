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
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.FileProviders;
using Backend.Config;

namespace Backend.Controllers
{
    [Route("science")]
    public class ScienceExportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly string _backend;
        private readonly DirectoryOptions _directoryOptions;

        public ScienceExportController(IConfiguration config, ApplicationDbContext context, IMapper mapper, DirectoryOptions directoryOptions)
        {
            _context = context;
            _mapper = mapper;
            _backend = config.GetSection("Global").GetRequiredSetting("Backend");
            _directoryOptions = directoryOptions;
        }

        [HttpPost("export")]
        public IActionResult ExportTrigger([FromBody] ScienceExportDto scienceExport)
        {
            /*
             * Stuff to export:
             * - Database content (outstanding)
             * - Exported folders
             * - Experiment files
             */
            string fileId = $"export{new Random().Next(10000, 99999):000000}";
            using (var stream = new FileStream(_directoryOptions.DataLocation + "Exports/" + fileId + ".zip", FileMode.CreateNew)) {

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    using (var file = new StreamWriter(archive.CreateEntry("readme.txt").Open()))
                    {
                        file.Write(scienceExport.Name + "\n\n" + scienceExport.Description + "\n\n");
                        file.Write("This export was generated with MACI version 0.9 at " + DateTime.Now.ToString());
                    }

                    foreach (int id in scienceExport.Experiments)
                    {
                        /* exported analysis files for this experiment id */
                        var tmp = _directoryOptions.DataLocation + $"/JupyterNotebook/sim{id:0000}";
                        foreach (var fileEntry in _directoryOptions.GetAllFilesRecursively(tmp))
                        {
                            archive.CreateEntryFromFile(fileEntry, $"JupyterNotebooks/sim{id:0000}/{fileEntry}");
                        }

                        // TODO copy experiment framework stuff to avoid overriding stuff
                        /* export experiment files */
                        var experiment = _context.Experiments.Where(s => id == s.Id).SingleOrDefault();
                        var tmp2 = _directoryOptions.DataLocation + $"/ExperimentFramework/" + experiment.FileName;
                        foreach (var fileEntry in _directoryOptions.GetAllFilesRecursively(tmp2))
                        {
                            archive.CreateEntryFromFile(fileEntry, $"ExperimentFramework/sim{id:0000}/{fileEntry}");
                        }
                    }
                }
                stream.Flush();
            }
            
            return Json(new
            {
                Name = fileId
            });
        }

        [HttpGet("{fileName}/export.zip")]
        public IActionResult ExportDownload(string fileName)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            /* Note: check old code with RootPath if this makes trouble */
            return PhysicalFile(_directoryOptions.DataLocation + "/Exports/" + fileName + ".zip", "application/zip", fileName + ".zip");
        }
    }
}
