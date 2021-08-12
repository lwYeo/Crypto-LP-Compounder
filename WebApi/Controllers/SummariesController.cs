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
    [Route("api/summary")]
    public class SummariesController : ControllerBase
    {
        public SummariesController(ILogger<SummariesController> _)
        {
        }

        [HttpGet]
        public IEnumerable<DTO.ISummary> Get()
        {
            return Program.GetSummaries?.Invoke();
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            DTO.ISummary summary =
                Program.GetSummaries?.Invoke()?
                .FirstOrDefault(s => s.InstanceName.Equals(id, StringComparison.OrdinalIgnoreCase));

            return summary == null ? NotFound() : Ok(new[] { summary });
        }
    }
}
