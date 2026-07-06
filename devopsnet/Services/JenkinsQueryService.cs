using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using devopsnet.Options;
using Microsoft.Extensions.Options;

namespace devopsnet.Services;

public class JenkinsQueryService
{
    private readonly HttpClient _httpClient;
    private readonly JenkinsOptions _options;

    public JenkinsQueryService(HttpClient httpClient, IOptions<JenkinsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        // Configuration de l'authentification Basic (identique à ton premier service)
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.ApiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    }

    private async Task<(string field, string value)> GetCrumbAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_options.BaseUrl.TrimEnd('/')}/crumbIssuer/api/json");
            if (!response.IsSuccessStatusCode) return (string.Empty, string.Empty);

            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            return (root.GetProperty("crumbRequestField").GetString() ?? "Jenkins-Crumb", root.GetProperty("crumb").GetString() ?? string.Empty);
        }
        catch { return (string.Empty, string.Empty); }
    }

    public async Task<string> GetUserPipelinesAsync(string username)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/job/{username}/api/json?tree=jobs[name,color,lastBuild[number,result,duration]]";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task TriggerBuildAsync(string username, string jobName)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/job/{username}/job/{jobName}/build";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var (field, value) = await GetCrumbAsync();
        if (!string.IsNullOrEmpty(field)) request.Headers.Add(field, value);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePipelineAsync(string username, string jobName)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/job/{username}/job/{jobName}/doDelete";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var (field, value) = await GetCrumbAsync();
        if (!string.IsNullOrEmpty(field)) request.Headers.Add(field, value);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GetBuildLogsAsync(string username, string jobName)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/job/{username}/job/{jobName}/lastBuild/logText/progressiveText?start=0";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}