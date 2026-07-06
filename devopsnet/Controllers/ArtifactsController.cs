using System;
using System.Threading.Tasks;
using devopsnet.Services;
using Microsoft.AspNetCore.Mvc;

namespace devopsnet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArtifactsController : ControllerBase
    {
        private readonly NexusService _nexusService;

        public ArtifactsController(NexusService nexusService)
        {
            _nexusService = nexusService;
        }

        [HttpGet("{username}")]
        public async Task<IActionResult> GetUserArtifacts(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username))
                {
                    return BadRequest("Le nom d'utilisateur est requis.");
                }

                var artifacts = await _nexusService.GetArtifactsForUserAsync(username);
                return Ok(artifacts);
            }
            catch (Exception ex)
            {
                // Alerte en cas de problème de communication avec Jenkins ou Nexus
                return StatusCode(500, $"Erreur lors de la récupération des artefacts : {ex.Message}");
            }
        }
    }
}