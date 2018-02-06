using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoMapper;
using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Backend.Data.Transfer;
using Backend.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using Backend.Config;

namespace Backend.Controllers
{
    [Route("experiments/{simid:int}/instances/{instanceid:int}", Name = "ExperimentInstance")]
    public class ExperimentInstanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly DirectoryOptions _directoryOptions;

        private ExperimentInstance _currentExperimentInstance;

        public ExperimentInstanceController(ApplicationDbContext context, IMapper mapper, DirectoryOptions directoryOptions)
        {
            _context = context;
            _mapper = mapper;
            _directoryOptions = directoryOptions;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var simId = int.Parse((string)context.RouteData.Values["simid"]);
            var instanceId = int.Parse((string)context.RouteData.Values["instanceid"]);

            var instance = _context.ExperimentInstances
                .Include(si => si.Experiment)
                .SingleOrDefault(i => (i.Id == instanceId) && (i.ExperimentId == simId));

            if (instance == null)
            {
                context.Result = NotFound();
                return;
            }

            _currentExperimentInstance = instance;

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public ExperimentInstanceDto Get()
        {
            // Load all related data for filling the ExperimentInstanceDto.
            _context.Experiments.Where(s => s.Id == _currentExperimentInstance.ExperimentId).Load();
            _context.Workers.Where(w => w.Token == _currentExperimentInstance.AssignedWorkerToken).Load();
            _context.ExperimentParameterAssignments.Where(a => a.ExperimentInstanceId == _currentExperimentInstance.Id)
                .Include(a => a.ParameterValue)
                .Load();
            _context.Parameters.Where(p => p.ExperimentId == _currentExperimentInstance.ExperimentId).Load();
            _context.LogMessages.Where(m => m.ExperimentInstanceId == _currentExperimentInstance.Id).Load();

            return new ExperimentInstanceDto
            {
                ExperimentId = _currentExperimentInstance.ExperimentId,
                Id = _currentExperimentInstance.Id,
                Configuration = _currentExperimentInstance.ParameterValues.Select(v => v.ParameterValue)
                    .ToDictionary(parameterValue => parameterValue.Parameter.Name, parameterValue => ParseUtils.ParseToClosestPossibleValueType(parameterValue.Value)),
                Status = _currentExperimentInstance.Status,
                AssignedServer = _currentExperimentInstance.AssignedWorker?.ConnectionInfo ?? "",
                Log = _currentExperimentInstance.Log,
                LogMessages = _currentExperimentInstance.LogMessages.MapTo<LogMessageDto>(_mapper)
            };
        }

        private ParameterValue getExperimentIdParameter()
        {
            return new ParameterValue
            {
                Parameter = new Parameter
                {
                    Name = "simId",
                    Type = ParameterType.String
                },
                Value = _currentExperimentInstance.Experiment.Id.ToString()
            };
        }

        private ParameterValue getExperimentInstanceIdParameter()
        {
            return new ParameterValue
            {
                Parameter = new Parameter
                {
                    Name = "simInstanceId",
                    Type = ParameterType.String
                },
                Value = _currentExperimentInstance.Id.ToString()
            };
        }

        private void copySnapshotFilesToArchive(ZipArchive archive)
        {
            var experimentFileName = _directoryOptions.DataLocation + $"/Experiments/sim{_currentExperimentInstance.ExperimentId:0000}";

            var filesGeneral = new DirectoryInfo(experimentFileName).GetFiles();
            foreach (var fileEntry in filesGeneral)
            {
                archive.CreateEntryFromFile(fileEntry.FullName, fileEntry.Name);
            }
        }

        [HttpGet("experiment.zip")]
        public IActionResult DownloadJobArchive()
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                copySnapshotFilesToArchive(archive);

                var selectedParameterInstances = _context.ExperimentParameterAssignments
                    .Where(a => a.ExperimentInstanceId == _currentExperimentInstance.Id)
                    .Include(a => a.ParameterValue).ThenInclude(i => i.Parameter)
                    .ToList()
                    .Select(a => a.ParameterValue)
                    .ToList();

                selectedParameterInstances.Add(getExperimentIdParameter());
                selectedParameterInstances.Add(getExperimentInstanceIdParameter());

                if (_currentExperimentInstance.Experiment.Language.ToLower() == "python")
                {
                    PackPythonSpecifics(archive, selectedParameterInstances);

                    using (var file = new StreamWriter(archive.CreateEntry("config.json").Open()))
                    {
                        file.Write("{\"timeout\" : " + _currentExperimentInstance.Experiment.Timeout.ToString() + "}");
                    }
                }
                else 
                {
                    throw new Exception("Unexpected experiment language.");
                }
            }

            return File(stream.ToArray(), "application/zip");
        }

        private void PackPythonSpecifics(ZipArchive archive, List<ParameterValue> selectedParameterInstances)
        {
            var experiment = _context.Experiments.Single(s => s.Id == _currentExperimentInstance.ExperimentId);
            var script = experiment.Script;
            var scriptInstall = experiment.ScriptInstall;

            var angularReplaced = new List<string>();

            using (var file = new StreamWriter(archive.CreateEntry("experiment.py").Open()))
            {
                /* angular style for parameter */
                foreach (var param in selectedParameterInstances)
                {
                    var angularStyle = "{{" + param.Parameter.Name + "}}";

                    if (script.Contains(angularStyle))
                    {
                        /* angular style does not encapsulate strings in ' ' */
                        script = script.Replace(angularStyle, param.Value);
                        angularReplaced.Add(param.Parameter.Name);
                    }
                }
                file.Write(script);
            }

            if (!String.IsNullOrEmpty(scriptInstall))
            {
                using (var file = new StreamWriter(archive.CreateEntry("install.py").Open()))
                {
                    file.Write(scriptInstall);
                }
            }

            using (var file = new StreamWriter(archive.CreateEntry("parameters.py").Open()))
            {
                file.Write("params = {");
                foreach (var param in selectedParameterInstances)
                {
                    var value = param.Parameter.Type == ParameterType.String ? $"'{param.Value}'" : param.Value;
                    file.Write($"'{param.Parameter.Name}' : {value}");

                    if (!ReferenceEquals(param, selectedParameterInstances.Last()))
                    {
                        file.Write(", ");
                    }
                }
                file.Write("}\n");

                file.Write("requestedParams = set(['simId', 'simInstanceId'");
                foreach(var param in angularReplaced)
                {
                    file.Write(", '" + param + "'");
                }
                file.Write("])\n");
            }
        }

        [HttpPost("reset")]
        public IActionResult ResetExperimentInstance()
        {
            _currentExperimentInstance.Reset();

            _context.SaveChanges();

            return Ok();
        }

        [HttpPut("error")]
        public IActionResult ReportError([FromHeader(Name = "Worker-Token")] string token,
            [FromBody] ExperimentInstanceErrorReportDto errorReport)
        {
            if (_currentExperimentInstance.AssignedWorkerToken != token)
            {
                return StatusCode(403, "This worker is not registered to this experiment.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _currentExperimentInstance.Log = errorReport.ErrorLog;
            _currentExperimentInstance.AssignedWorkerToken = null;
            _currentExperimentInstance.Status = ExperimentStatus.Error;
            _currentExperimentInstance.WorkFinished = DateTime.UtcNow;

            _context.SaveChanges();

            return Ok();
        }

        [HttpGet("records")]
        public IEnumerable<RecordDto> GetRecords()
        {
            var records = _context.Records.Where(r => r.ExperimentInstanceId == _currentExperimentInstance.Id).ToList();

            return records.MapTo<RecordDto>(_mapper);
        }

        [HttpPut("results")]
        public IActionResult SetResults([FromHeader(Name = "Worker-Token")] string token,
            [FromBody] ExperimentInstanceSuccessReportDto results)
        {
            const bool useMarcelExperimental = false;

            /* Performance statistics */
            Stopwatch sw = new Stopwatch();
            sw.Start();

            if (!ModelState.IsValid)
            {
                if(token == null)
                {
                    Console.WriteLine("Token is null");
                } else
                {
                    Console.WriteLine("Token looks good: " + token);
                }
                foreach(var k in ModelState.Keys)
                {
                    Console.WriteLine(k.ToString() + " -> " );
                    
                    foreach (var e in ModelState[k].Errors)
                    {
                        Console.WriteLine("\t -> " + e.ErrorMessage);
                        Console.WriteLine("\t -> " + e.Exception);
                    }
                    if (ModelState[k].AttemptedValue != null)
                        Console.WriteLine("for: " + ModelState[k].AttemptedValue.ToString());
                    else
                        Console.WriteLine("no value");
                }
                return BadRequest(ModelState);
            }

            TimeSpan ts = sw.Elapsed;
            Console.WriteLine("Checked model state in " + String.Format("{0:00}:{1:00}:{2:00}",
                ts.Hours, ts.Minutes, ts.Seconds));

            if (_currentExperimentInstance.AssignedWorkerToken != token)
            {
                return StatusCode(403, "This worker is not registered to this experiment.");
            }

            var recordsAlreadyExist = _context.Records.Any(r => r.ExperimentInstanceId == _currentExperimentInstance.Id);
            if (recordsAlreadyExist)
            {
                return BadRequest("Records have already been set.");
            }

            _currentExperimentInstance.Log = results.Log;
            _currentExperimentInstance.WorkFinished = DateTime.UtcNow;

            _currentExperimentInstance.LogMessages = new List<LogMessage>();
            foreach (var message in results.LogMessages.MapTo<LogMessage>(_mapper))
            {
                _currentExperimentInstance.LogMessages.Add(message);
            }

            _currentExperimentInstance.AssignedWorkerToken = null;
            _currentExperimentInstance.Status = ExperimentStatus.Finished;

            ts = sw.Elapsed;
            Console.WriteLine("Managed meta data in " + String.Format("{0:00}:{1:00}:{2:00}",
                ts.Hours, ts.Minutes, ts.Seconds));

            if (!useMarcelExperimental)
            {
                _currentExperimentInstance.Records = results.Records.MapTo<Record>(_mapper).ToList();
            }

            _context.SaveChanges();

            ts = sw.Elapsed;
            Console.WriteLine("Saved DB state in " + String.Format("{0:00}:{1:00}:{2:00}",
                ts.Hours, ts.Minutes, ts.Seconds));

            if (useMarcelExperimental)
            {
                foreach (var record in results.Records.MapTo<Record>(_mapper))
                {
                    _context.Database.ExecuteSqlCommand("INSERT INTO \"Records\" (\"Key\", \"Key2\", \"Key3\", \"Offset\", \"ExperimentInstanceId\", \"Value\") VALUES({0}, {1}, {2}, {3}, {4}, {5});",
                        record.Key, record.Key2, record.Key3, record.Offset, _currentExperimentInstance.Id, record.Value);
                }

                ts = sw.Elapsed;
                Console.WriteLine("Saved DB records in " + String.Format("{0:00}:{1:00}:{2:00}",
                    ts.Hours, ts.Minutes, ts.Seconds));
            }

            return Ok();
        }
    }
}
