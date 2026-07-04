using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace devopsnet.Templates;

public static class TemplateEngine
{
    public static async Task<string> GenerateJenkinsfileAsync(
        string technology,
        string jobName,
        string nexusRegistry,
        string credentialsId,
        Dictionary<string, string>? extraVariables = null)
    {
        string templateFileName = $"{technology}.template";
        string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName);

        if (!File.Exists(templatePath))
        {
            templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName);
        }

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Le template '{technology}' est introuvable.");
        }

        string content = await File.ReadAllTextAsync(templatePath);

        // Remplacement des variables globales obligatoires
        content = content.Replace("{JOB_NAME}", jobName)
                         .Replace("{NEXUS_REGISTRY}", nexusRegistry.TrimEnd('/'))
                         .Replace("{NEXUS_CREDENTIALS_ID}", credentialsId);

        // Injection des choix utilisateurs (Node version, dossier de sortie, etc.)
        if (extraVariables != null)
        {
            foreach (var (key, value) in extraVariables)
            {
                content = content.Replace($"{{{key}}}", value);
            }
        }

        return content;
    }
}