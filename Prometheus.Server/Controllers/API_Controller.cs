using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus.Server.Requests;
using System.Linq;

namespace Prometheus.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class API_Controller : ControllerBase
    {
        private readonly ILogger<API_Controller> _logger;

        public API_Controller(ILogger<API_Controller> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        [Route("/getblueprint")]
        public IActionResult GetBlueprint([FromBody] CheckStatusRequest id)
        {
            if (string.IsNullOrWhiteSpace(id.JobId))
            {
                return BadRequest("Job id cant be empty");
            }
            if (Prometheus.CompletedWork.Count > 0)
            {
                // Find blueprint
                foreach (var job in Prometheus.CompletedWork.ToArray())
                {
                    var blueprint = new BlueprintCompleteResponse { Blueprint = job.Value.Blueprint };
                    if (job.Key == id.JobId && job.Value.Blueprint != string.Empty)
                    {
                        return Ok(blueprint);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No completed work found");
            }
            return BadRequest("No blueprint found!");
        }
        [HttpPost]
        [Route("/checkstatus")]
        public ActionResult CheckJobStatus([FromBody] CheckStatusRequest id)
        {
            if (string.IsNullOrWhiteSpace(id.JobId))
            {
                return BadRequest("Job id cant be empty");
            }
            // Find status
            foreach (var job in Prometheus.JobStatus.ToArray())
            {
                var status = new StatusResponse { Status = job.Value };
                if (job.Key == id.JobId)
                {
                    return Ok(status);
                }
            }
            return BadRequest("No job found!");
        }

        [HttpPost]
        [Route("/requestblueprint")]
        public IActionResult RequestBlueprint([FromBody] RequestBluepint prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt.Prompt))
            {
                return BadRequest("Prompt cant be empty");
            }
            if (prompt.Prompt.Length > 5000)
            {
                return BadRequest("Prompt too long, max 5000 characters");
            }
            var jobid = Prometheus.GenerateJobId();
            Prometheus.AddJob(jobid, prompt.Prompt);
            return Ok(new BlueprintJobResponse { JobId = jobid });
        }
    }
}
