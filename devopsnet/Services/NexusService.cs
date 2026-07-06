using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using devopsnet.Models;
using devopsnet.Options;
using Microsoft.Extensions.Options;

namespace devopsnet.Services
{
    public class NexusService
    {
        private readonly HttpClient _httpClient;
        private readonly NexusOptions _options;
        private readonly JenkinsQueryService _jenkinsQueryService;

        public NexusService(
            HttpClient httpClient,
            IOptions<NexusOptions> options,
            JenkinsQueryService jenkinsQueryService)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _jenkinsQueryService = jenkinsQueryService;

            // Configuration de l'authentification Basic avec le compte admin global
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}")
            );
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<List<ArtifactProjectDto>> GetArtifactsForUserAsync(string username)
        {
            // 1. Récupérer les pipelines Jenkins de l'utilisateur
            var jenkinsJson = await _jenkinsQueryService.GetUserPipelinesAsync(username);
            var userPipelines = ExtractPipelineNames(jenkinsJson);

            // 2. Récupérer tous les composants Docker depuis le dépôt Nexus docker-private
            var allComponents = await GetAllNexusComponentsAsync();

            // 3. Filtrer et regrouper par tag pour chaque pipeline de l'utilisateur
            var result = new List<ArtifactProjectDto>();

            foreach (var pipeline in userPipelines)
            {
                // Dans Nexus Docker, le nom du composant correspond au nom de l'image (ex: test-react)
                var matches = allComponents
                    .Where(c => string.Equals(c.Name, pipeline, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Version)
                    .Distinct()
                    .ToList();

                if (matches.Any())
                {
                    result.Add(new ArtifactProjectDto
                    {
                        PipelineName = pipeline,
                        Tags = matches
                    });
                }
            }

            return result;
        }

        private async Task<List<NexusComponent>> GetAllNexusComponentsAsync()
        {
            var components = new List<NexusComponent>();
            string? continuationToken = null;
            var baseUrl = _options.BaseUrl.TrimEnd('/');

            do
            {
                // Construction de l'URL de l'API REST de Nexus pour lister les composants
                var url = $"{baseUrl}/service/rest/v1/components?repository={_options.Repository}";
                if (!string.IsNullOrEmpty(continuationToken))
                {
                    url += $"&continuationToken={continuationToken}";
                }

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<NexusComponentsResponse>(json);

                if (data != null)
                {
                    components.AddRange(data.Items);
                    continuationToken = data.ContinuationToken;
                }
                else
                {
                    continuationToken = null;
                }

            } while (!string.IsNullOrEmpty(continuationToken)); // Gère la pagination si tu as beaucoup d'images

            return components;
        }

        private List<string> ExtractPipelineNames(string jenkinsJson)
        {
            var names = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(jenkinsJson);
                if (doc.RootElement.TryGetProperty("jobs", out var jobsElement) && jobsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var job in jobsElement.EnumerateArray())
                    {
                        if (job.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                names.Add(name);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Retourne une liste vide en cas d'erreur de parsing globale
            }
            return names;
        }
    }
}