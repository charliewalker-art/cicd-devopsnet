using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace devopsnet.Models
{
    // Calque un composant renvoyé par l'API REST de Nexus
    public class NexusComponent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    // Réceptionne la liste complète et le token de pagination de Nexus
    public class NexusComponentsResponse
    {
        [JsonPropertyName("items")]
        public List<NexusComponent> Items { get; set; } = new();

        [JsonPropertyName("continuationToken")]
        public string? ContinuationToken { get; set; }
    }

    // L'objet final restructuré envoyé à ton React (Le nom du projet + ses tags accumulés)
    public class ArtifactProjectDto
    {
        public string PipelineName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }
}