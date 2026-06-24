using System.Net.Http.Headers;
using System.Text.Json;
using devopsnet.Data;
using devopsnet.Models;
using devopsnet.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace devopsnet.Services;

public class GitHubAuthService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly IDataProtector _protector;

    public GitHubAuthService(
        AppDbContext context,
        HttpClient httpClient,
        IOptions<GitHubOptions> options,
        IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _httpClient = httpClient;
        _options = options.Value;
        _protector = dataProtectionProvider.CreateProtector("GitHubAccessToken");
    }

    public async Task LinkAccountAsync(Guid userId, string code)
    {
        var accessToken = await ExchangeCodeForTokenAsync(code);
        var (gitHubId, gitHubUsername) = await GetGitHubProfileAsync(accessToken);

        var existing = await _context.GitHubAccounts.ByUserId(userId).FirstOrDefaultAsync();
        var encryptedToken = _protector.Protect(accessToken);

        if (existing is not null)
        {
            existing.GitHubId = gitHubId;
            existing.GitHubUsername = gitHubUsername;
            existing.EncryptedAccessToken = encryptedToken;
            existing.ConnectedAt = DateTime.UtcNow;
        }
        else
        {
            _context.GitHubAccounts.Add(new GitHubAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GitHubId = gitHubId,
                GitHubUsername = gitHubUsername,
                EncryptedAccessToken = encryptedToken,
                ConnectedAt = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GetDecryptedTokenAsync(Guid userId)
    {
        var account = await _context.GitHubAccounts.ByUserId(userId).FirstOrDefaultAsync();

        if (account is null)
            throw new InvalidOperationException("Aucun compte GitHub lié à cet utilisateur.");

        return _protector.Unprotect(account.EncryptedAccessToken);
    }

    private async Task<string> ExchangeCodeForTokenAsync(string code)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.CallbackUrl,
        });

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        if (!json.TryGetProperty("access_token", out var tokenProp))
            throw new InvalidOperationException("Échange du code OAuth2 échoué.");

        return tokenProp.GetString()!;
    }

    private async Task<(long GitHubId, string Username)> GetGitHubProfileAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("devopsnet", "1.0"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        return (json.GetProperty("id").GetInt64(), json.GetProperty("login").GetString()!);
    }




    public async Task<bool> IsLinkedAsync(Guid userId)
    {
        return await _context.GitHubAccounts.ByUserId(userId).AnyAsync();
    }


    public async Task UnlinkAccountAsync(Guid userId)
    {
        var account = await _context.GitHubAccounts.ByUserId(userId).FirstOrDefaultAsync();

        if (account is null)
            throw new InvalidOperationException("Aucun compte GitHub lié à cet utilisateur.");

        _context.GitHubAccounts.Remove(account);
        await _context.SaveChangesAsync();
    }


}