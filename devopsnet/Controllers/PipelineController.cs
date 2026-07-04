using System;
using System.Threading.Tasks;
using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Models;
using devopsnet.Services;
using devopsnet.Templates;
using devopsnet.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Security.Claims;

namespace devopsnet.Controllers;

[Authorize]
[ApiController]
[Route("api/pipelines")]
public class PipelineController : ControllerBase
{
    private readonly JenkinsManagerService _jenkinsService;
    private readonly GitHubAuthService _gitHubAuthService;
    private readonly AppDbContext _context;
    private readonly NexusOptions _nexusOptions;

    public PipelineController(
        JenkinsManagerService jenkinsService,
        GitHubAuthService gitHubAuthService,
        AppDbContext context,
        IOptions<NexusOptions> nexusOptions)
    {
        _jenkinsService = jenkinsService;
        _gitHubAuthService = gitHubAuthService;
        _context = context;
        _nexusOptions = nexusOptions.Value;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePipeline([FromBody] CreatePipelineDto dto)
    {
        var userId = GetCurrentUserId();

        try
        {
            // 1. Récupération du token GitHub de l'utilisateur
            string githubToken = await _gitHubAuthService.GetDecryptedTokenAsync(userId);
            string jenkinsFolder = User.Identity?.Name ?? "charliewalker";

            // 2. Préparation des variables spécifiques selon le choix du formulaire React
            var extraVariables = new Dictionary<string, string>();
            if (dto.Technology == "React")
            {
                extraVariables.Add("NODE_VERSION", dto.NodeVersion);
                extraVariables.Add("OUTPUT_DIR", dto.OutputDir);
            }

            // 3. Génération dynamique du Jenkinsfile via le moteur de template
            string jenkinsfileContent = await TemplateEngine.GenerateJenkinsfileAsync(
                dto.Technology,
                dto.Name,
                _nexusOptions.Registry,
                _nexusOptions.CredentialsId,
                extraVariables
            );

            // 4. Création du Job Jenkins en lui fournissant directement le script généré
            await _jenkinsService.CreateJenkinsJobAsync(jenkinsFolder, dto.Name, dto.CloneUrl, dto.Branch, githubToken, jenkinsfileContent);

            // 5. Sauvegarde en Base de données locale
            var pipeline = new Pipeline
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = dto.Name,
                CloneUrl = dto.CloneUrl,
                Branch = dto.Branch,
                Technology = dto.Technology
            };

            _context.Pipelines.Add(pipeline);
            await _context.SaveChangesAsync();

            var response = new PipelineResponseDto
            {
                Id = pipeline.Id,
                Name = pipeline.Name,
                CloneUrl = pipeline.CloneUrl,
                Branch = pipeline.Branch,
                Technology = pipeline.Technology,
                CreatedAt = pipeline.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Erreur lors de la création du pipeline : {ex.Message}" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}