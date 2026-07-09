using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace devopsnet.Services
{
    public class ArgoCDService : IArgoCDService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ArgoCDService> _logger;
        private readonly AppDbContext _context;
        private readonly IGitAutomationService _gitService;

        public ArgoCDService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ArgoCDService> logger,
            AppDbContext context,
            IGitAutomationService gitService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _gitService = gitService;
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

                var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/applications";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
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

        public async Task<int> CreateApplicationAsync(ArgoApplicationCreateDto dto)
        {
            try
            {
                var baseUrl = _configuration["ArgoCD:BaseUrl"];
                var token = _configuration["ArgoCD:Token"];
                var gitRepoUrl = _configuration["ArgoCD:LocalRepoUrl"];
                var startPortStr = _configuration["K3S_NODEPORT_START"] ?? "30080";
                int startPort = int.Parse(startPortStr);

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(gitRepoUrl))
                {
                    throw new InvalidOperationException("Configuration manquante dans le fichier .env pour l'initialisation du déploiement.");
                }

                // 1. Calcul du prochain NodePort via ton extension
                int maxPort = _context.Pipelines.GetMaxNodePort(startPort);
                int nextPort = maxPort + 1;
                _logger.LogInformation("Prochain port affecté : {Port}", nextPort);

                // 2. Appel du service Git (Template + Push)
                await _gitService.GenerateAndPushManifestAsync(dto.Name, dto.NexusImage, nextPort);

                // 3. Payload pour Argo CD avec fix du mot clé réservé '@namespace'
                var argoPayload = new
                {
                    metadata = new { name = dto.Name },
                    spec = new
                    {
                        project = "default",
                        source = new
                        {
                            repoURL = gitRepoUrl,
                            targetRevision = "HEAD",
                            path = "."
                        },
                        destination = new
                        {
                            server = "https://kubernetes.default.svc",
                            @namespace = "default" // ✅ Préservé avec le '@'
                        },
                        syncPolicy = new
                        {
                            automated = new { prune = true, selfHeal = true }
                        }
                    }
                };

                var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/applications";

                var jsonPayload = JsonSerializer.Serialize(argoPayload);

                // 💡 FIX EXACT : On instancie sans spécifier l'encodage pour ne pas forcer le charset
                var httpContent = new StringContent(jsonPayload);

                // 💡 FIX EXACT : On surcharge manuellement avec "application/json" pur
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = httpContent
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("L'API Argo CD a retourné un code d'échec : {Status}. Détails : {Details}", response.StatusCode, errorContent);
                    throw new Exception($"Échec de la création sur Argo CD : {response.StatusCode}");
                }

                // 4. Enregistrement en base PostgreSQL
                var newPipeline = new Pipeline
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId,
                    Name = dto.Name,
                    CloneUrl = $"{gitRepoUrl}/{dto.Name}-deployment.yml",
                    Branch = "master",
                    Technology = "Docker/Nexus via GitOps",
                    NodePort = nextPort,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Pipelines.Add(newPipeline);
                await _context.SaveChangesAsync();

                return nextPort;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur durant le flux de création de l'application {AppName}", dto.Name);
                throw;
            }
        }
    }
}