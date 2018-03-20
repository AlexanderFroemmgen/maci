using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Backend.Config;
using Backend.Data.Persistence;
using Backend.Data.Transfer;
using Backend.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;

namespace Backend.Controllers
{
    [Route("experiments")]
    public class ExperimentDataExportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHostingEnvironment _env;
        private readonly IMapper _mapper;
        private readonly ExportOptions _options;

        public ExperimentDataExportController(ApplicationDbContext context, IHostingEnvironment env, IOptions<ExportOptions> exportOptions, IMapper mapper)
        {
            _context = context;
            _env = env;
            _mapper = mapper;
            _options = exportOptions.Value;
        }

        [HttpGet("{id}/data.json")]
        public IActionResult GetResultData(int id)
        {
            var data =
                _context.Experiments
                    .Include(s => s.Parameters).ThenInclude(p => p.Values)
                    .Include(s => s.ExperimentInstances).ThenInclude(i => i.ParameterValues)
                    .Include(s => s.ExperimentInstances).ThenInclude(i => i.Records)
                    .Single(s => s.Id == id);

            if (data == null)
            {
                return NotFound();
            }
            
            var metadata = new
            {
                Parameters = data.Parameters.MapTo<ParameterDto>(_mapper)
            };
            
            var dataArray = new List<IDictionary<string, object>>();

            foreach (var simInstance in data.ExperimentInstances)
            {
                var simInstanceDataObject = new ExpandoObject() as IDictionary<string, object>;

                foreach (var paramInstance in simInstance.ParameterValues.Select(a => a.ParameterValue))
                {
                    var paramValue = ParseUtils.ParseToClosestPossibleValueType(paramInstance.Value);

                    simInstanceDataObject.Add(paramInstance.Parameter.Name, paramValue);
                }

                var recordArray = new JArray();
                foreach (var record in simInstance.Records)
                {
                    var recordValue = ParseUtils.ParseToClosestPossibleValueType(record.Value);

                    recordArray.Add(JObject.FromObject(new { record.Key, record.Offset, Value = recordValue }));
                }
                simInstanceDataObject.Add("records", recordArray);

                dataArray.Add(simInstanceDataObject);
            }

            return Json(new
            {
                Metadata = metadata,
                Data = dataArray
            });
        }

        private void GetResultDataCSVExport(int id, string path)
        {
            /* 
             * Performance notes:
             * 
             * This lazy style is much faster than loading all data at once.
             * 
             */
            var data =
                _context.Experiments
                    .Include(s => s.Parameters).ThenInclude(p => p.Values)
                    .Include(s => s.ExperimentInstances)
                    .Single(s => s.Id == id);

            var outputFileStream = new StreamWriter(new FileStream(path, FileMode.Create));

            var result = new StringBuilder("simInstanceId;");

            foreach (var parameter in data.Parameters.OrderBy(p => p.Name))
            {
                result.Append(parameter.Name);
                result.Append(";");
            }
            result.Append("value;offset;key;key2;key3;\n");
            outputFileStream.Write(result);            
            result.Clear();

            int counter = -1;
            int total = data.ExperimentInstances.Count();
            var sb = new StringBuilder();

            foreach (var simInstanceBlank in data.ExperimentInstances)
            {
                var simInstance = _context.ExperimentInstances
                    .Include(i => i.ParameterValues)
                    .Include(i => i.Records)
		   // .Include(i => i.LogMessages) Denny
                    .Single(i => i.Id == simInstanceBlank.Id);
                /* 
                 * Design desicion: 
                 * 
                 * All records are in a own row and share the same parameters.
                 * 
                 */

                var parameterStringBuilder = new StringBuilder(simInstance.Id + ";");

                foreach (var paramInstance in simInstance.ParameterValues
                    .OrderBy(pv => pv.ParameterValue.Parameter.Name)
                    .Select(a => a.ParameterValue))
                {
                    parameterStringBuilder.Append(paramInstance.Value);
                    parameterStringBuilder.Append(";");
                }

                var parameterString = parameterStringBuilder.ToString();
                foreach (var record in simInstance.Records)
                {
                    sb.Append(parameterString);
                    sb.Append(record.Value);
                    sb.Append(";");
                    sb.Append(record.Offset);
                    sb.Append(";");
                    sb.Append(record.Key);
                    sb.Append(";");
                    sb.Append(record.Key2);
                    sb.Append(";");
                    sb.Append(record.Key3);
                    sb.Append(";\n");
                }

                /* add failed statistics */
                sb.Append(parameterString);
                sb.Append(simInstance.Status == Data.Persistence.Model.ExperimentStatus.Finished ? 1 : 0);
                sb.Append(";0;Finished;\n");

                sb.Append(parameterString);
                sb.Append((simInstance.Status == Data.Persistence.Model.ExperimentStatus.Error || 
                    simInstance.Status == Data.Persistence.Model.ExperimentStatus.Aborted) ? 1 : 0);
                sb.Append(";0;Error;\n");


                /* denny string */

                /*                var log = simInstance.LogMessages.Where(lm => lm.Key == "Measurement result").SingleOrDefault();

                        if(log != null) {
                            var logStr = log.Message.Replace(";", "|");

                                sb.Append(parameterString);
                                sb.Append("DennyAdaptationLog");
                                sb.Append(";");
                                sb.Append(0);
                                sb.Append(";");
                                sb.Append(logStr);
                                sb.Append(";\n");
                        } else {
                            Console.WriteLine("No log message for " + simInstance.Id.ToString());
                        }*/

                /* end denny */

                counter++;
                if (counter % 50 == 0)
                {
                    Console.WriteLine("Exported " + counter.ToString() + " of " + total.ToString());
                    outputFileStream.Write(sb);
                    sb.Clear();
                }
            }
            /* write remaining stuff */
            outputFileStream.Write(sb);
            outputFileStream.Close();

            Console.WriteLine("Finished Export");
        }

        [HttpGet("{id}/metadata.json")]
        public IActionResult GetResultMetaData(int id)
        {
            var data =
                _context.Experiments
                    .Include(s => s.Parameters).ThenInclude(p => p.Values)
                    .Single(s => s.Id == id);

            if (data == null)
            {
                return NotFound();
            }

            var parameters = data.Parameters.MapTo<ParameterDto>(_mapper).Append(new ParameterDto
            {
                Name = "simInstanceId",
                Purpose = Data.Persistence.Model.ParameterPurpose.Environment,
                Type = Data.Persistence.Model.ParameterType.Int,
                Unit = string.Empty
            }); 

            var metadata = new
            {
                Parameters = parameters
            };

            return Json(metadata);
        }

        [HttpPost("{id}/exportNotebook/{force}")]
        public IActionResult ExportJupyterNotebookFiles(int id, bool force)
        {
            var experiment = _context.Experiments.Single(s => s.Id == id);

            if (experiment == null)
            {
                return NotFound();
            }
            
            var metadata = GetResultMetaData(id) as JsonResult;

            if (metadata == null)
            {
                return NotFound();
            }

            var fileName = $"sim{id:0000}";
            var exportDir = Path.Combine(_env.ContentRootPath, _options.JupyterNotebookExportPath, fileName);

            if(System.IO.File.Exists(Path.Combine(exportDir, Path.GetFileName(_options.JupyterNotebookBaseFile))) && !force)
            {
                return new ObjectResult(new { ExportDir = exportDir, FileName = fileName, Message = "File Exists" });
            }

            Directory.CreateDirectory(exportDir);
            System.IO.File.Copy(
                Path.Combine(_env.ContentRootPath, _options.JupyterNotebookBaseFile), 
                Path.Combine(exportDir, Path.GetFileName(_options.JupyterNotebookBaseFile)), 
                true);

            GetResultDataCSVExport(id, Path.Combine(exportDir, "data.csv"));
            System.IO.File.WriteAllText(Path.Combine(exportDir, "metadata.json"), JsonConvert.SerializeObject(metadata.Value));

            return new ObjectResult(new { ExportDir = exportDir, FileName = fileName });
        }
    }
}
