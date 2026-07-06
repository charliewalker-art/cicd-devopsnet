using LibGit2Sharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace devopsnet.Services
{
    public class GitAutomationService : IGitAutomationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GitAutomationService> _logger;

        public GitAutomationService(IConfiguration configuration, ILogger<GitAutomationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task GenerateAndPushManifestAsync(string appName, string nexusImage, int nodePort)
        {
            var localRepoPath = _configuration["ArgoCD:LocalRepoPath"]
                ?? throw new InvalidOperationException("La variable ArgoCD:LocalRepoPath est manquante.");
            var remoteRepoUrl = _configuration["ArgoCD:LocalRepoUrl"]
                ?? throw new InvalidOperationException("La variable ArgoCD:LocalRepoUrl est manquante.");

            // ==========================================
            // ÉTAPE 1 : Option B - Gestion du Clone Automatique
            // ==========================================
            if (!Directory.Exists(localRepoPath) || !Repository.IsValid(localRepoPath))
            {
                _logger.LogInformation("Le dossier local n'est pas un dépôt Git valide. Initialisation du clone depuis {Url}...", remoteRepoUrl);

                if (Directory.Exists(localRepoPath))
                {
                    Directory.Delete(localRepoPath, true); // On nettoie si c'était un dossier fantôme
                }

                Repository.Clone(remoteRepoUrl, localRepoPath);
                _logger.LogInformation("Dépôt cloné avec succès dans : {Path}", localRepoPath);
            }

            // ==========================================
            // ÉTAPE 2 : Lecture et injection dans le Template
            // ==========================================
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "KubernetesManifest.template");

            // Si jamais AppContext.BaseDirectory ne trouve pas en dev, on cherche dans le dossier racine
            if (!File.Exists(templatePath))
            {
                templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "KubernetesManifest.template");
            }

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Le fichier template est introuvable à l'adresse : {templatePath}");
            }

            string templateContent = await File.ReadAllTextAsync(templatePath);

            // Remplacement des placeholders comme ton TemplateEngine
            string finalYaml = templateContent
                .Replace("{{AppName}}", appName)
                .Replace("{{NexusImage}}", nexusImage)
                .Replace("{{NodePort}}", nodePort.ToString());

            // Écriture du fichier final directement dans le dépôt Git local
            string fileName = $"{appName}-deployment.yml";
            string filePath = Path.Combine(localRepoPath, fileName);
            await File.WriteAllTextAsync(filePath, finalYaml);

            // ==========================================
            // ÉTAPE 3 : Git Commit & Push via LibGit2Sharp
            // ==========================================
            try
            {
                using (var repo = new Repository(localRepoPath))
                {
                    // git add [nom_du_fichier]
                    Commands.Stage(repo, fileName);

                    // git commit -m "..."
                    var signature = new Signature("devopsnet-api", "api@devops.local", DateTimeOffset.Now);
                    Commit commit = repo.Commit($"feat: auto-deploy {appName} on port {nodePort}", signature, signature);
                    _logger.LogInformation("Commit validé localement. Hash : {Hash}", commit.Sha);

                    // git push origin master
                    var remote = repo.Network.Remotes["origin"];
                    var options = new PushOptions();
                    repo.Network.Push(remote, @"refs/heads/master", options);
                    _logger.LogInformation("Manifeste poussé avec succès sur la VM Git !");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec des opérations Git pour l'application {AppName}", appName);
                throw;
            }
        }
    }
}