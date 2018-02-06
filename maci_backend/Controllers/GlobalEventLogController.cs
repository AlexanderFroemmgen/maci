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

namespace Backend.Controllers
{
    [Route("event_log")]
    public class GlobalEventLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        
        public GlobalEventLogController(IConfiguration config, ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var logs = _context.GlobalEventLogMessages.Select(gelm => new
            {
                Message = gelm.Message,
                Time = gelm.Time,
                Type = gelm.Type,
                ExperimentId = gelm.ExperimentId,
                ExperimentInstanceId = gelm.ExperimentInstanceId
            }).ToList();
            
            return new ObjectResult(logs);
        }
    }
}
