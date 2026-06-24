namespace devopsnet.Options;

public class CorsOptions
{
    public const string SectionName = "Cors";

    public string AllowedOrigin { get; set; } = string.Empty;
}