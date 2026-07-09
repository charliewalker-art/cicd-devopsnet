using devopsnet.Dto;
using devopsnet.Services;
using Microsoft.AspNetCore.Authorization; // 💡 Ajout pour [Authorize]
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims; // 💡 Ajout pour récupérer les Claims du Token
using System.Threading.Tasks;

namespace devopsnet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArgoCDController : ControllerBase
    {
        private readonly IArgoCDService _argoCDService;
        private readonly ILogger<ArgoCDController> _logger;

        public ArgoCDController(IArgoCDService argoCDService, ILogger<ArgoCDController> logger)
        {
            _argoCDService = argoCDService;
            _logger = logger;
        }

        // --- Ta méthode GET existante (api/argocd) ---
        [HttpGet]
        public async Task<IActionResult> GetApplications()
        {
            var apps = await _argoCDService.GetAllApplicationsAsync();
            return Ok(apps);
        }

        // =========================================================================
        //  NOUVELLE ROUTE SÉCURISÉE : POST api/argocd/deploy
        // =========================================================================
        [HttpPost("deploy")]
        [Authorize] // 💡 Bloque la requête si l'utilisateur n'envoie pas son Token Bearer
        public async Task<IActionResult> DeployApplication([FromBody] ArgoApplicationCreateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.NexusImage))
            {
                return BadRequest("Le nom de l'application et l'image Nexus complète sont requis.");
            }

            try
            {
                // 💡 FIX : Extraction automatique de ton identifiant unique depuis le jeton de sécurité (Token JWT)
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                  ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Tentative de déploiement refusée : ID utilisateur introuvable dans le token.");
                    return Unauthorized("Impossible de récupérer ton identité utilisateur depuis le token.");
                }

                //  FIX : On force le vrai GUID récupéré du token dans le DTO pour écraser les zéros de React
                dto.UserId = Guid.Parse(userIdClaim);

                _logger.LogInformation("Nouvelle demande de déploiement reçue pour l'application : {AppName} par l'utilisateur : {UserId}", dto.Name, userIdClaim);

                // Appel du flux complet (Calcul port -> Git Push -> Argo CD API -> Sauvegarde SQL valide)
                int assignedPort = await _argoCDService.CreateApplicationAsync(dto);

                // On renvoie un succès avec le port généré pour que React puisse l'afficher
                return Ok(new
                {
                    message = $"L'application {dto.Name} a été configurée et transmise à Argo CD avec succès.",
                    port = assignedPort
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Erreur de configuration locale lors du déploiement de {AppName}", dto.Name);
                return StatusCode(500, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur critique lors du traitement du déploiement de {AppName}", dto.Name);
                return StatusCode(500, new { error = "Une erreur interne est survenue lors du déploiement GitOps." });
            }
        }
    }
}