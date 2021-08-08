using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InstancesController : ControllerBase
    {
        public InstancesController(ILogger<InstancesController> logger)
        {
        }

        [HttpGet]
        public IEnumerable<DTO.ICompounder> Get()
        {
            return Program.GetInstances?.Invoke();
        }
    }
}
