/*
   Copyright 2021 Lip Wee Yeo Amano

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/instance")]
    public class InstancesController : ControllerBase
    {
        public InstancesController(ILogger<InstancesController> _)
        {
        }

        [HttpGet]
        public IEnumerable<DTO.ICompounder> Get()
        {
            return Program.GetInstances?.Invoke();
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            DTO.ICompounder instance =
                Program.GetInstances?.Invoke()?
                .FirstOrDefault(s => s.InstanceName.Equals(id, StringComparison.OrdinalIgnoreCase));

            return instance == null ? NotFound() : Ok(new[] { instance });
        }
    }
}
