using System.Diagnostics;
using System.IO.Compression;

namespace devopsnet.Services;

public class GitCloneService
{
    public async Task<string> CloneAsync(string cloneUrl, string branch, string accessToken, Guid userId)
    {
        var targetPath = Path.Combine(Path.GetTempPath(), "devopsnet-clones", userId.ToString(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(targetPath);

        var authenticatedUrl = cloneUrl.Replace("https://", $"https://{accessToken}@");

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"clone --branch {branch} --single-branch \"{authenticatedUrl}\" \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossible de démarrer le processus git.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Échec du clonage : {error}");
        }

        return targetPath;
    }

    public string CompressToZip(string sourcePath, string repoName)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "devopsnet-clones", $"{repoName}-{Guid.NewGuid()}.zip");

        ZipFile.CreateFromDirectory(sourcePath, zipPath);

        return zipPath;
    }
}