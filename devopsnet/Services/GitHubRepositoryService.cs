using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using devopsnet.Dto;

namespace devopsnet.Services;

public class GitHubRepositoryService
{
    private readonly HttpClient _httpClient;

    public GitHubRepositoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<RepositoryResponseDto>> GetUserRepositoriesAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/repos?per_page=100&visibility=all");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("devopsnet", "1.0"));

        var response = await _httpClient.SendAsync(request);

        // Correction ici : On intercepte l'erreur sans faire crasher l'application
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"L'API GitHub a refusé l'accès ({response.StatusCode}) : {errorContent}");
        }

        var repos = await response.Content.ReadFromJsonAsync<JsonElement>();

        var result = new List<RepositoryResponseDto>();
        foreach (var repo in repos.EnumerateArray())
        {
            result.Add(new RepositoryResponseDto
            {
                Name = repo.GetProperty("name").GetString()!,
                FullName = repo.GetProperty("full_name").GetString()!,
                CloneUrl = repo.GetProperty("clone_url").GetString()!,
                IsPrivate = repo.GetProperty("private").GetBoolean(),
                DefaultBranch = repo.GetProperty("default_branch").GetString()!,
            });
        }

        return result;
    }




}