namespace devopsnet.Options; 
public class NexusOptions
{
    public const string SectionName = "Nexus";

    public string Registry { get; set; } = string.Empty;
    public string CredentialsId { get; set; } = string.Empty;
}