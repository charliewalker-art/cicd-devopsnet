using devopsnet.Dto;
using devopsnet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IO;

namespace devopsnet.Controllers;

[ApiController]
[Authorize]
[Route("api/repositories")]
public class RepositoryController : ControllerBase
{
    private readonly GitHubAuthService _gitHubAuthService;
    private readonly GitHubRepositoryService _gitHubRepositoryService;
    private readonly GitCloneService _gitCloneService;

    public RepositoryController(
        GitHubAuthService gitHubAuthService,
        GitHubRepositoryService gitHubRepositoryService,
        GitCloneService gitCloneService)
    {
        _gitHubAuthService = gitHubAuthService;
        _gitHubRepositoryService = gitHubRepositoryService;
        _gitCloneService = gitCloneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRepositories()
    {
        var userId = GetCurrentUserId();

        try
        {
            var token = await _gitHubAuthService.GetDecryptedTokenAsync(userId);
            var repos = await _gitHubRepositoryService.GetUserRepositoriesAsync(token);
            return Ok(repos);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("clone")]
    public async Task<IActionResult> Clone([FromBody] CloneRequestDto dto)
    {
        var userId = GetCurrentUserId();

        try
        {
            var token = await _gitHubAuthService.GetDecryptedTokenAsync(userId);
            var cloneUrl = $"https://github.com/{dto.RepoFullName}.git";
            var clonedPath = await _gitCloneService.CloneAsync(cloneUrl, dto.Branch, token, userId);

            var repoName = dto.RepoFullName.Split('/').Last();
            var zipPath = _gitCloneService.CompressToZip(clonedPath, repoName);

            var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

            // Nettoyage sécurisé des fichiers
            System.IO.File.Delete(zipPath);
            DeleteReadOnlyDirectory(clonedPath); // Utilisation de la nouvelle méthode ici

            return File(zipBytes, "application/zip", $"{repoName}.zip");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }

    /// <summary>
    /// Supprime un dossier en supprimant d'abord l'attribut Lecture Seule de tous ses fichiers.
    /// </summary>
    private void DeleteReadOnlyDirectory(string targetDir)
    {
        if (!Directory.Exists(targetDir)) return;

        // Supprime l'attribut lecture seule sur tous les fichiers
        var files = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            System.IO.File.SetAttributes(file, FileAttributes.Normal);
        }

        // Supprime l'attribut lecture seule sur tous les sous-dossiers si nécessaire
        var dirs = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories);
        foreach (var dir in dirs)
        {
            System.IO.File.SetAttributes(dir, FileAttributes.Normal);
        }

        // Suppression finale du dossier principal
        Directory.Delete(targetDir, true);
    }
}