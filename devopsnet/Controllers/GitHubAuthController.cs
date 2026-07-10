using devopsnet.Dto;
using devopsnet.Options;
using devopsnet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace devopsnet.Controllers;

[ApiController]
[Route("api/auth/github")]
public class GitHubAuthController : ControllerBase
{
    private readonly GitHubAuthService _gitHubAuthService;
    private readonly GitHubOptions _options;
    private readonly IConfiguration _configuration;

    public GitHubAuthController(
        GitHubAuthService gitHubAuthService,
        IOptions<GitHubOptions> options,
        IConfiguration configuration)
    {
        _gitHubAuthService = gitHubAuthService;
        _options = options.Value;
        _configuration = configuration;
    }

    [Authorize]
    [HttpGet("login")]
    public IActionResult Login()
    {
        var userId = GetCurrentUserId();

        var url = "https://github.com/login/oauth/authorize" +
                  $"?client_id={_options.ClientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(_options.CallbackUrl)}" +
                  "&scope=repo" +
                  $"&state={Uri.EscapeDataString(userId.ToString())}";

        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] GitHubCallbackDto dto, [FromQuery] string state)
    {
        var frontendUrl = _configuration["Cors:AllowedOrigin"];

        if (!Guid.TryParse(state, out var userId))
        {
            return Redirect($"{frontendUrl}/repositories?github=error&message=Etat+invalide.");
        }

        try
        {
            await _gitHubAuthService.LinkAccountAsync(userId, dto.Code);
            return Redirect($"{frontendUrl}/repositories?github=linked");
        }
        catch (InvalidOperationException ex)
        {
            return Redirect($"{frontendUrl}/repositories?github=error&message={Uri.EscapeDataString(ex.Message)}");
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }

    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = GetCurrentUserId();
        var isLinked = await _gitHubAuthService.IsLinkedAsync(userId);
        return Ok(new { isLinked });
    }


    [Authorize]
    [HttpDelete("unlink")]
    public async Task<IActionResult> Unlink()
    {
        var userId = GetCurrentUserId();

        try
        {
            await _gitHubAuthService.UnlinkAccountAsync(userId);
            return Ok(new { message = "Compte GitHub déconnecté." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}