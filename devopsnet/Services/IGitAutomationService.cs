using System.Threading.Tasks;

namespace devopsnet.Services
{
    public interface IGitAutomationService
    {
        Task GenerateAndPushManifestAsync(string appName, string nexusImage, int nodePort);
    }
}