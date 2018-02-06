using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Backend.Data.Transfer;
using Backend.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Backend.WorkerHost;
using Microsoft.Extensions.Caching.Memory;
using Backend.Config;

namespace Backend.Controllers
{
    [Route("experiments")]
    public class ExperimentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly WorkerHostService _hostService;
        private readonly ScalingService _scalingService;
        private readonly IMemoryCache _cache;
        private readonly DirectoryOptions _directoryOptions;

        public ExperimentController(ApplicationDbContext context, IMapper mapper, WorkerHostService hostService, ScalingService scalingService, IMemoryCache memoryCache, DirectoryOptions directoryOptions)
        {
            _context = context;
            _mapper = mapper;
            _hostService = hostService;
            _scalingService = scalingService;
            _cache = memoryCache;
            _directoryOptions = directoryOptions;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            /* 
             * This is a workaround. Making a single statement here fails. 
             * https://github.com/aspnet/EntityFrameworkCore/issues/5522 
             */
            var tmp = _context.Experiments
                    .Include(s => s.ExperimentInstances).ToList().Select(s => new
                    {
                        Id = s.Id,
                        FileName = s.FileName,
                        RunName = s.RunName,
                        Created = s.Created,
                        LastAssigned = s.ExperimentInstances.Count != 0 ? s.ExperimentInstances.OrderBy(si => si.WorkStarted).Last().WorkStarted : new DateTime(0),
                        Status = GlobalExperimentStateResolver.Resolve(s),
                        Statistics = s.ExperimentInstances.Count != 0 ? s.ExperimentInstances.GroupBy(si => si.Status).ToDictionary(gr => gr.Key, gr => gr.Count()) : new Dictionary<ExperimentStatus, int>()
                    });

            var result = new ObjectResult(
                tmp);

            return result;
        }


        /* 
         * Design discussion:
         * 
         * In the original design,
         * we used a single get method to retrieve all experiment data at once.
         * However, this was painfully slow for very large experiments due to
         * the O/R mapping and the data transfer.
         */

        [HttpGet("{id}")]
        public IActionResult GetExperimentStub(int id)
        {
            var data =
                _context.Experiments
                    .Where(s => s.Id == id)
                    .SingleOrDefault();

            if (data == null)
            {
                return NotFound();
            }

            /* Required to avoid mapper error */
            data.ExperimentInstances = new List<ExperimentInstance>();
            data.Parameters = new List<Parameter>();

            var result = data.MapTo<ExperimentDto>(_mapper);
            
            return new ObjectResult(result);
        }

        [HttpGet("{id}/parameters")]
        public IActionResult GetExperimentParameters(int id)
        {
            var data =
                _context.Experiments
                    .Where(s => s.Id == id)
                    .Include(s => s.Parameters).ThenInclude(p => p.Values)
                    .SingleOrDefault();

            if (data == null)
            {
                return NotFound();
            }

            /* Required to avoid mapper error */
            data.ExperimentInstances = new List<ExperimentInstance>();

            var result = data.MapTo<ExperimentDto>(_mapper);

            return new ObjectResult(result.Parameters);
        }

        [HttpGet("{id}/{pageIndex}")]
        public IActionResult GetExperimentInstances(int id, int pageIndex)
        {
            var experimentInstances =
                _context.Experiments
                    .Where(s => s.Id == id)
                    .Include(s => s.ExperimentInstances)
                        .ThenInclude(i => i.ParameterValues)
                            .ThenInclude(pv => pv.ParameterValue)
                                .ThenInclude(pv => pv.Parameter)
                    .Include(s => s.ExperimentInstances)
                        .ThenInclude(i => i.LogMessages)
                    //.AsNoTracking() // TODO: does this provide a performance benefit? add measurement stuff
                    .SingleOrDefault()
                    .ExperimentInstances;

            if (experimentInstances == null)
            {
                return NotFound();
            }

            var result = experimentInstances.MapTo<ExperimentInstanceDto>(_mapper);

            /* Do not transfer log messages, but generate aggregated HasWarning info */
            foreach (var si in result)
            {
                si.HasWarnings = si.LogMessages.Where(lm => lm.Type == LogMessageType.Warning).Any();
                si.LogMessages = null;
            }

            const int pageSize = 50;
            var paginagedInstances = PaginatedList<ExperimentInstanceDto>.Create(result, pageIndex, pageSize);
            return new ObjectResult(new
            {
                ExperimentInstances = paginagedInstances,
                TotalExperimentInstances = paginagedInstances.TotalEntries,
                TotalPages = paginagedInstances.TotalPages,
                HasPreviousPage = paginagedInstances.HasPreviousPage,
                HasNextPage = paginagedInstances.HasNextPage,
                CurrentPage = pageIndex
            });
        }

        private void AddSeedParameter(Experiment experiment, int range)
        {
            var experimentParam = new Parameter
            {
                Name = "seed",
                Type = ParameterType.Int,
                Purpose = ParameterPurpose.Environment,
                Unit = String.Empty,
                Experiment = experiment
            };
            experimentParam.Values = Enumerable.Range(0, range).Select(i => new ParameterValue
            {
                Parameter = experimentParam,
                Value = i.ToString()
            }).ToList();

            experiment.Parameters.Add(experimentParam);
        }

        [HttpPost]
        public IActionResult Create([FromBody] ExperimentCreateDto requestData)
        {
            /* Performance statistics */
            Stopwatch sw = new Stopwatch();
            sw.Start();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (requestData.Parameters.Select(p => p.Name).Distinct().Count() != requestData.Parameters.Count)
            {
                return BadRequest("Parameter names are not unique.");
            }

            if (!requestData.Parameters.All(p => p.Values.All(v => ParameterValidator.IsValid(p.Type, v))))
            {
                return BadRequest("Parameter values are not valid.");
            }

            string errMsg = "";
            string msg = "";
            if (!ExecuteLocalMACIScript(requestData.FileName, ref msg, ref errMsg))
            {
                return new ObjectResult(new 
                {
                    Failed = true,
                    Message = msg,
                    ErrorMessage = errMsg
                });
            }

            // Create the experiment.
            var experiment = new Experiment
            {
                Created = DateTime.UtcNow,
                Script = requestData.Script,
                ScriptInstall = requestData.ScriptInstall,
                Parameters = new List<Parameter>(),
                RequiredCapabilities = requestData.RequiredCapabilities,
                Language = requestData.Language,
                PermutationFilter = requestData.PermutationFilter,
                Repetitions = requestData.Repetitions,
                RunName = requestData.RunName,
                FileName = requestData.FileName,
                Timeout = 60 // in minutes 
            };

            // Create parameters and parameter values.
            foreach (var parameterDto in requestData.Parameters)
            {
                var experimentParam = new Parameter
                {
                    Name = parameterDto.Name,
                    Type = parameterDto.Type,
                    Purpose = parameterDto.Purpose,
                    Unit = parameterDto.Unit,
                    Experiment = experiment,
                    Values = new List<ParameterValue>()
                };

                experiment.Parameters.Add(experimentParam);

                foreach (var value in parameterDto.Values)
                {
                    experimentParam.Values.Add(new ParameterValue
                    {
                        Parameter = experimentParam,
                        Value = value
                    });
                }
            }

            AddSeedParameter(experiment, requestData.Seeds);

            // Create one ExperimentInstance for each unique combination of parameters.
            var instances = FindAllInstancePermutations(experiment.Parameters);

            TimeSpan ts = sw.Elapsed;
            Console.WriteLine("Generated " + instances.Count() + " experiment instances in " + String.Format("{0:00}:{1:00}:{2:00}",
                ts.Hours, ts.Minutes, ts.Seconds));

            try
            {
                instances = FilterPermutations(instances, experiment.PermutationFilter);
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }

            ts = sw.Elapsed;
            Console.WriteLine("Filtered to " + instances.Count() + " experiment instances in " + String.Format("{0:00}:{1:00}:{2:00}",
                ts.Hours, ts.Minutes, ts.Seconds));

            instances = DuplicateExperimentInstances(instances, requestData.Repetitions);

            if (!instances.Any())
            {
                return BadRequest("No experiment instances generated after filtering and duplicating.");
            }

            if(requestData.TestRun)
            {
                instances = instances.Take(1).ToList();
                instances.ForEach(i => i.Priority = 100);
                experiment.RunName = "TEST: " + experiment.RunName;
            }

            experiment.ExperimentInstances = instances;

            // Add everything to the database.
            _context.Add(experiment);
            _context.SaveChanges();


            /* Event logging */
            TimeSpan totalTime = sw.Elapsed;
            _context.Add(new GlobalEventLogMessage
            {
                Message = String.Format("Created Experiment {0:0} with {1:0} instances in {2:00}:{3:00}:{4:00}",
                    experiment.Id,
                    experiment.ExperimentInstances.Count(),
                    totalTime.Hours,
                    totalTime.Minutes,
                    totalTime.Seconds),
                ExperimentId = experiment.Id,
                Time = DateTime.Now,
                Type = GlobalEventLogMessageType.Info,
                ExperimentInstanceId = -1
            });
            _context.SaveChanges();


            /* Create folder and copy relevant files (persistent snapshot) 
             * after storing in database (storing generates the experimentId) */
            CreatePersistentSnapshotFolder(experiment);

            _scalingService.Scale(experiment);

            return new ObjectResult(new
            {
                ExperimentId = experiment.Id,
                ExperimentInstanceId = experiment.ExperimentInstances.First().Id
            });
        }

        [HttpPost("createStub")]
        public IActionResult CreateStub([FromBody] ExperimentCreateDto requestData)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (requestData.Parameters.Select(p => p.Name).Distinct().Count() != requestData.Parameters.Count)
            {
                return BadRequest("Parameter names are not unique.");
            }

            // Create the experiment.
            var experiment = new Experiment
            {
                Created = DateTime.UtcNow,
                Script = requestData.Script,
                ScriptInstall = requestData.ScriptInstall,
                Parameters = new List<Parameter>(),
                RequiredCapabilities = requestData.RequiredCapabilities,
                Language = requestData.Language,
                PermutationFilter = requestData.PermutationFilter,
                Repetitions = requestData.Repetitions,
                RunName = requestData.RunName,
                FileName = requestData.FileName,
                Timeout = 60 // in minutes 
            };

            // Create parameters and parameter values.
            foreach (var parameterDto in requestData.Parameters)
            {
                var experimentParam = new Parameter
                {
                    Name = parameterDto.Name,
                    Type = parameterDto.Type,
                    Purpose = parameterDto.Purpose,
                    Unit = parameterDto.Unit,
                    Experiment = experiment,
                    Values = new List<ParameterValue>()
                };

                experiment.Parameters.Add(experimentParam);
            }
            
            // Add everything to the database.
            _context.Add(experiment);
            _context.SaveChanges();

            /* Create folder and copy relevant files (persistent snapshot) 
             * after storing in database (storing generates the experimentId) */
            CreatePersistentSnapshotFolder(experiment);

            return new ObjectResult(new
            {
                ExperimentId = experiment.Id
            });
        }

        private bool ExecuteLocalMACIScript(string experimentFileName, ref string msg, ref string errMsg)
        {
            var scriptPath = _directoryOptions.DataLocation + "/ExperimentTemplates/" + experimentFileName + "/" + "update_maci.bat";
            if (new FileInfo(scriptPath).Exists)
            {
                var p = Process.Start(new ProcessStartInfo(scriptPath)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
                p.WaitForExit(1000 * 60); // 1 minute timeout
                if (!p.HasExited)
                {
                    msg = "MACI Backend Timeout after 1 Minute";
                    errMsg = "";
                    return false;
                }

                if(p.ExitCode != 0)
                {
                    msg = p.StandardOutput.ReadToEnd();
                    errMsg = p.StandardError.ReadToEnd();
                    return false;
                }
            }

            return true;
        }

        private void CreatePersistentSnapshotFolder(Experiment experiment)
        {
            // TODO: In case a file exists in the general and the specific framework folder, we get an exception.

            var targetFolder = _directoryOptions.DataLocation + $"/Experiments/sim{experiment.Id:0000}";
            var di = new DirectoryInfo(targetFolder);
            if(di.Exists)
            {
                di.MoveTo(_directoryOptions.DataLocation + $"/Experiments/sim{experiment.Id:0000}_removed_at_{DateTime.Now:ddMMyy_Hmmss}");
                di = new DirectoryInfo(targetFolder);
            }

            di.Create();

            /* general experiment files */
            var fileNameGeneral = _directoryOptions.DataLocation + "/ExperimentFramework";
            
            foreach (var fileEntry in new DirectoryInfo(fileNameGeneral).GetFiles())
            {
                fileEntry.CopyTo(targetFolder + "/" + fileEntry.Name);
            }

            /* files for this experiment fileName */
            var fileName = _directoryOptions.DataLocation + "/ExperimentTemplates/" + experiment.FileName + "/framework";
            _directoryOptions.CopyDirectoryRecursively(new DirectoryInfo(fileName), targetFolder);
        }

        private List<ExperimentInstance> FindAllInstancePermutations(IEnumerable<Parameter> parameters)
        {
            var instances = new List<ExperimentInstance>();
            FindAllInstancePermutations_Recursion(
                instances,
                new List<Parameter>(parameters),
                new List<ParameterValue>());
            return instances;
        }

        private void FindAllInstancePermutations_Recursion(
            IList<ExperimentInstance> experimentInstances,
            IList<Parameter> parameters,
            IList<ParameterValue> selectedParams)
        {
            // If there are no parameters left in the list, we have selected a value for each parameter.
            if (!parameters.Any())
            {
                var experimentInstance = new ExperimentInstance
                {
                    ParameterValues = new List<ExperimentParameterAssignment>(),
                    Status = ExperimentStatus.Pending
                };

                experimentInstances.Add(experimentInstance);

                foreach (var selectedParam in selectedParams)
                {
                    experimentInstance.ParameterValues.Add(new ExperimentParameterAssignment
                    {
                        ExperimentInstance = experimentInstance,
                        ParameterValue = selectedParam
                    });
                }
            }
            else
            {
                // Pop the first parameter from the list.
                var parameter = parameters.First();
                parameters.Remove(parameter);

                // Select each possible value once.
                foreach (var paramInstance in parameter.Values)
                {
                    selectedParams.Add(paramInstance);
                    FindAllInstancePermutations_Recursion(experimentInstances, parameters, selectedParams);
                    selectedParams.Remove(paramInstance);
                }

                parameters.Insert(0, parameter);
            }
        }
        
        private List<ExperimentInstance> FilterPermutations(List<ExperimentInstance> instances, string permutationFilter)
        {
            if (string.IsNullOrWhiteSpace(permutationFilter))
            {
                return instances;
            }

            /* 
            * This code is performance senstive, just Roslyn is to slow.
            * 
            * We replace the parameters in the string. Starting with the 
            * longest parameter ensures that we do not replace shorter once accidently.
            * 
            * Pre-sorting or hashing strings did not increase the performance in first tests, 
            * thus, we stick to the simpler code.
            */
                        
            var results = new List<ExperimentInstance>();
            foreach (var instance in instances)
            {
                var pf = permutationFilter;

                foreach (var parameterAssignment in instance.ParameterValues.OrderBy(p => -1 * p.ParameterValue.Parameter.Name.Length))
                {
                    var paramType = parameterAssignment.ParameterValue.Parameter.Type;
                    var variableName = parameterAssignment.ParameterValue.Parameter.Name;
                    var variableValue = parameterAssignment.ParameterValue.Value;

                    if (parameterAssignment.ParameterValue.Parameter.Type == ParameterType.String)
                    {
                        variableValue = $"\"{variableValue}\"";
                    }
                    else if (parameterAssignment.ParameterValue.Parameter.Type == ParameterType.Float) {
                        variableValue = $"{variableValue}f";
                    }
                    pf = pf.Replace(variableName, variableValue);
                }
                
                try
                {
                    var script = CSharpScript.Create<bool>(pf);
                    if (!script.RunAsync().Result.ReturnValue)
                    {
                        /* skip for FALSE evaluations */
                        continue;
                    }

                    results.Add(instance);
                }
                catch (CompilationErrorException e)
                {
                    throw new Exception("Invalid permutation filter.\n" + e.Diagnostics.First().GetMessage());
                }
            }
            return results;
        }

        private List<ExperimentInstance> DuplicateExperimentInstances(List<ExperimentInstance> instances,
            int repetitions)
        {
            var copies = new List<ExperimentInstance>();

            for (var i = 0; i < repetitions; i++)
            {
                foreach (var instance in instances)
                {
                    var instanceCopy = new ExperimentInstance
                    {
                        ParameterValues = new List<ExperimentParameterAssignment>()
                    };

                    foreach (var valueAssignment in instance.ParameterValues)
                    {
                        instanceCopy.ParameterValues.Add(new ExperimentParameterAssignment
                        {
                            ParameterValue = valueAssignment.ParameterValue,
                            ExperimentInstance = instanceCopy
                        });
                    }

                    copies.Add(instanceCopy);
                }
            }

            return copies;
        }

        [HttpPost("{id}/addParameterValue")]
        public IActionResult AddParameterValue(int id, [FromBody] ParameterValueAddDto parameterValueAddDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var sim = _context.Experiments
                .Include(s => s.Parameters).ThenInclude(p => p.Values)
                .Include(s => s.ExperimentInstances)
                .SingleOrDefault(s => s.Id == id);

            if (sim == null)
            {
                return NotFound();
            }

            var changedParameter = sim.Parameters.SingleOrDefault(p => p.Name == parameterValueAddDto.ParameterName);

            if (changedParameter == null)
            {
                return BadRequest("Invalid parameter name.");
            }

            if (changedParameter.Values.Any(v => v.Value == parameterValueAddDto.Value))
            {
                return BadRequest("Parameter value already exists.");
            }

            if (!ParameterValidator.IsValid(changedParameter.Type, parameterValueAddDto.Value))
            {
                return BadRequest("Parameter value is invalid.");
            }

            var addedParameterValue = new ParameterValue
            {
                Parameter = changedParameter,
                Value = parameterValueAddDto.Value
            };

            changedParameter.Values.Add(addedParameterValue);

            // Find all permutations for all parameters that have not been changed.
            // At this point, all of these experiment instances miss an assignment for the changed parameter!
            var unchangedParameters = sim.Parameters.Where(p => p.Name != parameterValueAddDto.ParameterName);
            var experimentInstances = FindAllInstancePermutations(unchangedParameters);

            // Add the new parameter value to each experiment instance.
            foreach (var experimentInstance in experimentInstances)
            {
                experimentInstance.ParameterValues.Add(new ExperimentParameterAssignment
                {
                    ParameterValue = addedParameterValue,
                    ExperimentInstance = experimentInstance
                });
            }

            experimentInstances = FilterPermutations(experimentInstances, sim.PermutationFilter);

            experimentInstances = DuplicateExperimentInstances(experimentInstances, sim.Repetitions);


            if (!experimentInstances.Any())
            {
                return BadRequest("No experiment instances generated after filtering and duplicating.");
            }

            foreach (var experimentInstance in experimentInstances)
            { 
                sim.ExperimentInstances.Add(experimentInstance);
            }

            _context.SaveChanges();

            _scalingService.Scale(sim);

            return Ok();
        }

        [HttpPost("{id}/addCombination")]
        public IActionResult AddCombination(int id, [FromBody] List<ParameterValueAddDto> parameterValueAddDto)
        {
            /*
             * This method does not ensure a full cartesian product.
             */

            var performanceTracking = false;
            Stopwatch sw = performanceTracking ? new Stopwatch() : null;

            if(sw != null) 
                sw.Start();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if(sw != null)
            {
                TimeSpan ts = sw.Elapsed;
                Console.WriteLine("Model state validated after " + String.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            Experiment sim;

            if (!_cache.TryGetValue(id, out sim))
            {
                sim = _context.Experiments
                   .Include(s => s.Parameters).ThenInclude(p => p.Values)
                   .SingleOrDefault(s => s.Id == id);
                _cache.Set(id, sim, TimeSpan.FromMinutes(1));
            }

            if (sim == null)
            {
                Console.WriteLine("Server did not find experiment " + id.ToString());
                return NotFound();
            }

            if (sw != null)
            {
                TimeSpan ts = sw.Elapsed;
                Console.WriteLine("Loaded data after " + String.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            if (!parameterValueAddDto.TrueForAll(para => sim.Parameters.Single(p => p.Name == para.ParameterName) != null))
            {
                Console.WriteLine("Invalid parameter name " + parameterValueAddDto.Where(para => sim.Parameters.Single(p => p.Name == para.ParameterName) == null).First().ParameterName + ".");
                return BadRequest("Invalid parameter name " + parameterValueAddDto.Where(para => sim.Parameters.Single(p => p.Name == para.ParameterName) == null).First().ParameterName + ".");
            }

            /*if (sim.Parameters.Where(para => parameterValueAddDto.Exists(p => p.ParameterName != para.Name)).Count() > 0)
            {
                Console.WriteLine("Missing parameter " + sim.Parameters.Where(para => parameterValueAddDto.Exists(p => p.ParameterName != para.Name)).First().Name + ".");
                return BadRequest("Missing parameter " + sim.Parameters.Where(para => parameterValueAddDto.Exists(p => p.ParameterName != para.Name)).First().Name + ".");
            }*/

            if (!parameterValueAddDto.TrueForAll(para => ParameterValidator.IsValid(sim.Parameters.Single(p => p.Name == para.ParameterName).Type, para.Value)))
            {
                Console.WriteLine("Parameter value " +
                    parameterValueAddDto.Where(para => !ParameterValidator.IsValid(sim.Parameters.Single(p => p.Name == para.ParameterName).Type, para.Value)).First().Value +
                    " is invalid.");
                return BadRequest("Parameter value " + 
                    parameterValueAddDto.Where(para => !ParameterValidator.IsValid(sim.Parameters.Single(p => p.Name == para.ParameterName).Type, para.Value)).First().Value  + 
                    " is invalid.");
            }
            
            var experimentInstance = new ExperimentInstance();

            experimentInstance.ParameterValues = parameterValueAddDto.
                Select(para => new ExperimentParameterAssignment
                {
                    ParameterValue = new ParameterValue
                    {
                        Parameter = sim.Parameters.SingleOrDefault(p => p.Name == para.ParameterName),
                        Value = para.Value
                    },
                    ExperimentInstance = experimentInstance
                }).ToList();

            experimentInstance.ExperimentId = sim.Id;
            experimentInstance.Experiment = sim;

            /* 
             * The whole caching is kind of nasty. 
             * We might got sim from the cache, so make
             * sure to not add it without tracking.
             */

            if (sw != null)
            {
                TimeSpan ts = sw.Elapsed;
                Console.WriteLine("Prepared data for storage after " + String.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            _context.Attach(sim);
            _context.Add(experimentInstance);
            _context.SaveChanges();

            if (sw != null)
            {
                TimeSpan ts = sw.Elapsed;
                Console.WriteLine("Stored data after " + String.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            //_scalingService.Scale(sim);

            if (sw != null)
            {
                TimeSpan ts = sw.Elapsed;
                Console.WriteLine("Finished scaling after " + String.Format("{0:00}:{1:00}:{2:00}:{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds));
            }

            return new ObjectResult(new
            {
                ExperimentInstanceId = experimentInstance.Id
            });
        }

        public class Test
        {
            public string Status { get; set; }
        }

        [HttpPost("{id}/reset")]
        public IActionResult ResetExperiment(int id, [FromBody] Test withStatus)
        {
            var sim = _context.Experiments
                    .Include(s => s.ExperimentInstances) 
                    .Single(s => s.Id == id);

            if (sim == null)
            {
                return NotFound();
            }

            var selectedInstances = sim.ExperimentInstances;
            ExperimentStatus status;

            if (Enum.TryParse(withStatus.Status, out status))
            {
                selectedInstances = selectedInstances.Where(si => si.Status == status).ToList();
            }
            var selectedInstancesCount = selectedInstances.Count();

            foreach (var simInstance in selectedInstances)
            {
                /* ugly hack, kind of lazy loading */
                var simInstanceFull = _context.ExperimentInstances
                    .Include(i => i.LogMessages)
                    .Include(i => i.Records)
                    .Single(i => i.Id == simInstance.Id);
                simInstanceFull.Reset();
            }

            _context.SaveChanges();

            return new ObjectResult(new
            {
                Count = selectedInstancesCount
            });
        }

        [HttpPost("{id}/stoneClone")]
        public IActionResult StoneCloneExperiment(int id)
        {
            var experimentTemplate = _context.Experiments
                    .Include(s => s.ExperimentInstances).
                        ThenInclude(si => si.ParameterValues)
                    .Include(s => s.Parameters).
                        ThenInclude(p => p.Values)
                    .Single(s => s.Id == id);

            if (experimentTemplate == null)
            {
                return NotFound();
            }

            string errMsg = "";
            string msg = "";
            if (!ExecuteLocalMACIScript(experimentTemplate.FileName, ref msg, ref errMsg))
            {
                return new ObjectResult(new
                {
                    Failed = true,
                    Message = msg,
                    ErrorMessage = errMsg
                });
            }

            // Clone the experiment.
            var experimentClone = new Experiment
            {
                Created = DateTime.UtcNow,
                Script = experimentTemplate.Script,
                ScriptInstall = experimentTemplate.ScriptInstall,
                RequiredCapabilities = experimentTemplate.RequiredCapabilities,
                Language = experimentTemplate.Language,
                Parameters = new List<Parameter>(),
                PermutationFilter = experimentTemplate.PermutationFilter,
                Repetitions = experimentTemplate.Repetitions,
                RunName = "Clone of " + experimentTemplate.RunName,
                FileName = experimentTemplate.FileName,
                Timeout = experimentTemplate.Timeout
            };

            // Clone parameters and parameter values.
            foreach (var parameter in experimentTemplate.Parameters)
            {
                var experimentParam = new Parameter
                {
                    Name = parameter.Name,
                    Type = parameter.Type,
                    Purpose = parameter.Purpose,
                    Unit = parameter.Unit,
                    Experiment = experimentClone,
                    Values = new List<ParameterValue>()
                };

                experimentClone.Parameters.Add(experimentParam);

                foreach (var value in parameter.Values)
                {
                    experimentParam.Values.Add(new ParameterValue
                    {
                        Parameter = experimentParam,
                        Value = value.Value
                    });
                }
            }

            /* Clone experiment instances */
            experimentClone.ExperimentInstances = new List<ExperimentInstance>();
            foreach (var experimentInstance in experimentTemplate.ExperimentInstances)
            {
                experimentClone.ExperimentInstances.Add(new ExperimentInstance
                {
                    ParameterValues = experimentInstance.ParameterValues.
                        Select(pv => new ExperimentParameterAssignment { 
                        /* a bit complex due to reference handling :-( */
                            ParameterValue = experimentClone.Parameters.
                                Where(p => p.Name == pv.ParameterValue.Parameter.Name).
                                SelectMany(p => p.Values).
                                    Where(v => v.Value == pv.ParameterValue.Value).
                                    FirstOrDefault()
                        }).ToList(),
                    Experiment = experimentClone
                });
            }
            
            // Add everything to the database.
            _context.Add(experimentClone);
            _context.SaveChanges();

            /* Create folder and copy relevant files (persistent snapshot) 
             * after storing in database (storing generates the experimentId) */
            CreatePersistentSnapshotFolder(experimentClone);

            return new ObjectResult(new
            {
                ExperimentId = experimentClone.Id
            });
        }

        [HttpPost("{id}/abort")]
        public IActionResult AbortExperiment(int id)
        {
            var pendingInstances = _context.ExperimentInstances.Where(i => i.Status == ExperimentStatus.Pending && i.ExperimentId == id);

            foreach (var instance in pendingInstances)
            {
                instance.Status = ExperimentStatus.Aborted;
            }

            _context.SaveChanges();

            return Ok();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteExperiment(int id)
        {
            var sim = _context.Experiments.Single(s => s.Id == id);

            _context.Remove(sim);
            _context.SaveChanges();

            return Ok();
        }

        [HttpGet("{id}/remainingTime")]
        public IActionResult GetRemainingTime(int id)
        {
            var sim = _context.Experiments
                .Include(s => s.ExperimentInstances)
                .SingleOrDefault(s => s.Id == id);

            if (sim == null)
            {
                return NotFound();
            }

            var workStarted = sim.ExperimentInstances
                .Where(i => i.Status == ExperimentStatus.Finished || i.Status == ExperimentStatus.Error)
                .Select(i => i.WorkStarted)
                .DefaultIfEmpty().Min();

            var avgTimeSpan = TimeSpan.FromTicks(Convert.ToInt64(sim.ExperimentInstances
                .Where(i => i.Status == ExperimentStatus.Finished || i.Status == ExperimentStatus.Error)
                .Select(i => (i.WorkFinished - i.WorkStarted).Ticks)
                .DefaultIfEmpty().Average()));

            var activeWorkers =
                _context.Workers.Count(w => w.LastRequestTime > DateTime.UtcNow.AddTicks(-2 * avgTimeSpan.Ticks));

            var remainingExperiments =
                sim.ExperimentInstances.Count(
                    i => i.Status == ExperimentStatus.Pending || i.Status == ExperimentStatus.Running);

            var estimatedRemainingTime = TimeSpan.FromMilliseconds(-1);

            if (activeWorkers > 0)
            {
                estimatedRemainingTime = TimeSpan.FromTicks(avgTimeSpan.Ticks * remainingExperiments / activeWorkers);
            }

            var result = new
            {
                WorkStarted = workStarted,
                ElapsedTime = DateTime.UtcNow - workStarted,
                AverageTimeToCompletion = avgTimeSpan,
                ActiveWorkerCount = activeWorkers,
                RemainingExperimentCount = remainingExperiments,
                EstimatedRemainingTime = estimatedRemainingTime,
                EstimatedTimeOfCompletion = DateTime.UtcNow.Add(estimatedRemainingTime)
            };

            return new ObjectResult(result);
        }

        public class RequiredCapabilitiesHelper
        {
            public ICollection<string> Capabilities { get; set; }
        }

        [HttpPost("{id}/requiredCapabilities")]
        public IActionResult SetRequiredCapabilities(int id, [FromBody] RequiredCapabilitiesHelper requiredCapabilitiesHelper)
        {
            var sim = _context.Experiments
                    .Single(s => s.Id == id);

            if (sim == null)
            {
                return NotFound();
            }

            sim.RequiredCapabilities = requiredCapabilitiesHelper.Capabilities;            
            _context.SaveChanges();

            return Ok();
        }
        

        [HttpPost("{id}/timeout")]
        public IActionResult SetTimeout(int id, [FromBody] TimeoutDto timeoutDto)
        {
            var sim = _context.Experiments
                    .Single(s => s.Id == id);

            if (sim == null)
            {
                return NotFound();
            }

            sim.Timeout = timeoutDto.Timeout;
            _context.SaveChanges();

            return Ok();
        }
    }
}
