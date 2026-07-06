namespace devopsnet.Dto
{
    public class ArgoApplicationDto
    {
        public string Name { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = string.Empty; // ex: Healthy, Degraded
        public string SyncStatus { get; set; } = string.Empty;   // ex: Synced, OutOfSync
        public string RepoUrl { get; set; } = string.Empty;      // Ton Git local ou distant
        public string TargetRevision { get; set; } = string.Empty; // ex: HEAD
    }

    public class ArgoApplicationCreateDto
    {
        public string Name { get; set; } = string.Empty;       // Nom de l'app (ex: super-api)
        public string NexusImage { get; set; } = string.Empty; // Image complète (ex: 192.168.196.3:8111/super-api:1)
        public Guid UserId { get; set; }                        // L'ID de l'utilisateur qui déploie
    }

}
