namespace devopsnet.Options
{
    public class NexusOptions
    {
        public const string SectionName = "Nexus";

        public string Registry { get; set; } = string.Empty;
        public string CredentialsId { get; set; } = string.Empty;

        // Nouvelles propriétés pour l'API REST
        public string BaseUrl { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}