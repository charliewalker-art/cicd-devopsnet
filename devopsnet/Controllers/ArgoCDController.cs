using devopsnet.Services;
using Microsoft.AspNetCore.Mvc;


namespace devopsnet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArgoCDController : ControllerBase
    {
        private readonly IArgoCDService _argoCDService;

        public ArgoCDController(IArgoCDService argoCDService)
        {
            _argoCDService = argoCDService;
        }

        [HttpGet("applications")]
        public async Task<IActionResult> GetApplications()
        {
            var applications = await _argoCDService.GetAllApplicationsAsync();
            return Ok(applications);
        }
    }
}