using System.ComponentModel.DataAnnotations;

namespace devopsnet.Dto;

public class CreatePipelineDto
{
    public string Name { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";

 
    public string Technology { get; set; } = "HtmlStatic"; // Valeur par défaut
    public string NodeVersion { get; set; } = "20";        // Utilisé si Technology == "React"
    public string OutputDir { get; set; } = "dist";        // Utilisé si Technology == "React"
}