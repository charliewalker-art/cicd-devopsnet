using devopsnet.Dto;
using devopsnet.Models;
using devopsnet.Services;
using System.Net.Http.Headers;
using System.Text.Json;


namespace devopsnet.Services
{
    public class ArgoCDService : IArgoCDService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ArgoCDService> _logger;

        public ArgoCDService(HttpClient httpClient, IConfiguration configuration, ILogger<ArgoCDService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<ArgoApplicationDto>> GetAllApplicationsAsync()
        {
            try
            {
                var baseUrl = _configuration["ArgoCD:BaseUrl"];
                var token = _configuration["ArgoCD:Token"];

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token))
                {
                    _logger.LogError("La configuration Argo CD (BaseUrl ou Token) est manquante dans le fichier .env.");
                    throw new InvalidOperationException("Configuration Argo CD invalide.");
                }

                // Configuration de la requête HTTP
                var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/applications";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                // Injection du token Bearer généré sur l'interface Argo CD
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Échec de l'appel à l'API Argo CD. Statut : {StatusCode}", response.StatusCode);
                    return new List<ArgoApplicationDto>();
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                var rawData = JsonSerializer.Deserialize<ArgoApplicationListResponse>(jsonString);

                if (rawData == null || rawData.Items == null)
                {
                    return new List<ArgoApplicationDto>();
                }

                // Transformation des données brutes en DTOs propres pour React
                return rawData.Items.Select(item => new ArgoApplicationDto
                {
                    Name = item.Metadata.Name,
                    HealthStatus = item.Status.Health.Status,
                    SyncStatus = item.Status.Sync.Status,
                    RepoUrl = item.Spec.Source.RepoUrl,
                    TargetRevision = item.Spec.Source.TargetRevision
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Une erreur est survenue lors de la récupération des applications Argo CD.");
                return new List<ArgoApplicationDto>();
            }
        }
    }
}