using System.Text.Json.Serialization;

namespace devopsnet.Models
{
    public class ArgoApplicationListResponse
    {
        [JsonPropertyName("items")]
        public List<ArgoApplicationItem> Items { get; set; } = new();
    }

    public class ArgoApplicationItem
    {
        [JsonPropertyName("metadata")]
        public ArgoMetadata Metadata { get; set; } = new();

        [JsonPropertyName("spec")]
        public ArgoSpec Spec { get; set; } = new();

        [JsonPropertyName("status")]
        public ArgoStatus Status { get; set; } = new();
    }

    public class ArgoMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class ArgoSpec
    {
        [JsonPropertyName("source")]
        public ArgoSource Source { get; set; } = new();
    }

    public class ArgoSource
    {
        [JsonPropertyName("repoURL")]
        public string RepoUrl { get; set; } = string.Empty;

        [JsonPropertyName("targetRevision")]
        public string TargetRevision { get; set; } = string.Empty;
    }

    public class ArgoStatus
    {
        [JsonPropertyName("health")]
        public ArgoHealth Health { get; set; } = new();

        [JsonPropertyName("sync")]
        public ArgoSync Sync { get; set; } = new();
    }

    public class ArgoHealth
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class ArgoSync
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}