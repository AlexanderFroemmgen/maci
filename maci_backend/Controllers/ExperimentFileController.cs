using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using AutoMapper;
using Backend.Data.Persistence;
using Backend.Data.Persistence.Model;
using Backend.Data.Transfer;
using Backend.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text;
using LibGit2Sharp;
using Backend.Config;
using Microsoft.Extensions.Options;

namespace Backend.Controllers
{
    /**
     * Experiment Templates
     */
    [Route("experimentFiles")]
    public class ExperimentFileController : Controller
    {
        private readonly string relativeFolder;
        
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly DirectoryOptions _directoryOptions;
        private readonly GitRemoteOptions _gitRemoteOptions;

        private bool isValidPath(string path)
        {
            return Path.GetFullPath(path).StartsWith(Path.GetFullPath(relativeFolder));
        }
        
        public ExperimentFileController(ApplicationDbContext context, IMapper mapper, 
            DirectoryOptions directoryOptions, IOptions<GitRemoteOptions> gitRemoteOptions)
        {
            _context = context;
            _mapper = mapper;
            _directoryOptions = directoryOptions;
            _gitRemoteOptions = gitRemoteOptions.Value;

            relativeFolder = _directoryOptions.DataLocation + "/ExperimentTemplates";
        }

        [HttpGet]
        public IEnumerable<string> GetAll()
        {
            var result = new DirectoryInfo(relativeFolder).EnumerateDirectories()
                .Where(fi => !fi.Name.StartsWith(DeletedPrefix))
                .Select(fileInfo => fileInfo.Name);
            return result;
        }

        [HttpGet("{name}")]
        public ExperimentFileDto Get(string name)
        {
            var filename = relativeFolder + "/" + name + "/script.py";
            var filenameInstall = relativeFolder + "/" + name + "/install.py";
            var dirname = relativeFolder + "/" + name + "/configurations";

            if (isValidPath(filename))
            {
                var scriptInstall = "";

                try
                {
                    scriptInstall = _directoryOptions.GetFileContents(filenameInstall);
                } catch(IOException)
                {
                    // nothing to do
                }

                try
                {
                    return new ExperimentFileDto()
                    {
                        Name = name,
                        Script = _directoryOptions.GetFileContents(filename),
                        ScriptInstall = scriptInstall,
                        Configurations = new DirectoryInfo(dirname).EnumerateFiles()
                            .Where(fileInfo => !fileInfo.Name.StartsWith(DeletedPrefix))
                            .ToDictionary(f => f.Name.Replace(".json", ""), f => _directoryOptions.GetFileContents(f.FullName))
                    };
                } catch(IOException)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private static string DeletedPrefix = "__del_";

        private static string GetCurrentDeletedPrefix()
        {
            return DeletedPrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_";
        }
        

        [HttpPost("{name}/delete")]
        public IActionResult DeleteExperiment(string name) {
            var oldDirname = relativeFolder + "/" + name;
            var newDirname = relativeFolder + "/" + GetCurrentDeletedPrefix() + name;
            if (isValidPath(oldDirname)) {
                try
                {
                    new DirectoryInfo(oldDirname).MoveTo(newDirname);
                    return Ok();
                }
                catch (IOException ex)
                {
                    return StatusCode(500);
                }
            }
            else
            {
                return BadRequest("Invalid name.");
            }
        }

        [HttpPost]
        public IActionResult Create([FromBody] ExperimentFileDto requestData)
        {

            var dirname = relativeFolder + "/" + requestData.Name;
            var configDirname = dirname + "/configurations/";
            var filename = dirname + "/script.py";
            var filenameInstall = dirname + "/install.py";

            if (isValidPath(filename)) {
                new DirectoryInfo(dirname).Create();
                new DirectoryInfo(configDirname).Create();

                Repository.Init(dirname);

                _directoryOptions.SetFileContents(filename, requestData.Script);
                _directoryOptions.SetFileContents(filenameInstall, requestData.ScriptInstall);
                _directoryOptions.SetFileContents(configDirname + requestData.Configurations.First().Key + ".json", requestData.Configurations.First().Value);

                /* generate experiment framework file... */
                var frameworkFolder = new DirectoryInfo(dirname + "/framework");
                if(!frameworkFolder.Exists)
                {
                    frameworkFolder.Create();
                }

                GitIntegration.CreateBackup(dirname, "create/update experimentTemplate", _gitRemoteOptions);
                return Ok();
            }
            else
            {
                return BadRequest("Invalid name.");
            }
        }

        [HttpPost("{name}/configs/{configName}/delete")]
        public IActionResult DeleteConfig(string name, string configName)
        {
            var filename = relativeFolder + "/" + name + "/configurations/" + configName + ".json";
            if (isValidPath(filename))
            {
                new FileInfo(filename).Delete();
                GitIntegration.CreateBackup(relativeFolder + "/" + name, "delete config " + configName, _gitRemoteOptions);
                return Ok();
            }
            else
            {
                return BadRequest("Invalid name.");
            }
        }
        

        [HttpPost("{name}/configs")]
        public IActionResult CreateConfig(string name, [FromBody] ExperimentFileConfigDto requestData)
        {
            if(name == "undefined")
            {
                return BadRequest("Invalid name.");
            }

            var filename = relativeFolder + "/" + name + "/configurations/" + requestData.Name + ".json";
            if (isValidPath(filename))
            {
                _directoryOptions.SetFileContents(filename, requestData.Configuration);
                GitIntegration.CreateBackup(relativeFolder + "/" + name, "create/update config "+requestData.Name, _gitRemoteOptions);
                return Ok();
            }
            else
            {
                return BadRequest("Invalid name.");
            }
        }


        [HttpGet("{name}/history")]
        public ExperimentHistoryDto GetHistory(string name) {
            var repoDir = relativeFolder + "/" + name;
            
            if (isValidPath(repoDir)) {
                if(!Repository.IsValid(repoDir))
                {
                    Repository.Init(repoDir);
                }

                using (var repo = new Repository(repoDir)) {
                    var RFC2822Format = "ddd dd MMM HH:mm:ss yyyy K";
                    var history = new List<ExperimentHistoryDto.ExperimentHistoryItemDto>();
                    string page = HttpContext.Request.Query["page"].ToString();
                    int count = 15;
                    if (HttpContext.Request.Query.ContainsKey("count"))
                        Int32.TryParse(HttpContext.Request.Query["count"], out count);
                    foreach (Commit c in repo.Commits.Take(count))
                    {
                        var item = new ExperimentHistoryDto.ExperimentHistoryItemDto
                        {
                            CommitId = c.Id.ToString(),
                            Author = c.Author.Name + " <" + c.Author.Email + ">",
                            Date = c.Author.When.ToString(RFC2822Format, CultureInfo.InvariantCulture),
                            Message = c.Message
                        };
                        if (c.Parents.Count() > 1) {
                            item.MergeId = string.Join(" ", c.Parents.Select(p => p.Id.Sha.Substring(0, 7)).ToArray());
                        }
                        history.Add(item);
                    }
                    Remote remote = repo.Network.Remotes["origin"];
                    return new ExperimentHistoryDto
                    {
                        History = history,
                        RepoRemoteUrl = remote?.Url
                    };
                }
            } else {
                return null;
            }
        }

    }
}

