using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using devopsnet.Options;
using Microsoft.Extensions.Options;

namespace devopsnet.Services;

public class JenkinsManagerService
{
    private readonly HttpClient _httpClient;
    private readonly JenkinsOptions _options;

    public JenkinsManagerService(HttpClient httpClient, IOptions<JenkinsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Username}:{_options.ApiToken}")
        );
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
    }

    private async Task<(string crumbFieldName, string crumbValue)> GetCrumbAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_options.BaseUrl.TrimEnd('/')}/crumbIssuer/api/json");

            if (!response.IsSuccessStatusCode)
            {
                return (string.Empty, string.Empty);
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var fieldName = root.GetProperty("crumbRequestField").GetString() ?? "Jenkins-Crumb";
            var crumb = root.GetProperty("crumb").GetString() ?? string.Empty;

            return (fieldName, crumb);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private async Task<HttpResponseMessage> SendPostAsync(string relativeUrl, HttpContent content)
    {
        var (crumbField, crumbValue) = await GetCrumbAsync();
        var fullUrl = $"{_options.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";

        var request = new HttpRequestMessage(HttpMethod.Post, fullUrl)
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(crumbField) && !string.IsNullOrEmpty(crumbValue))
        {
            request.Headers.Add(crumbField, crumbValue);
        }

        return await _httpClient.SendAsync(request);
    }

    private static string ToBase64(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
    }

    private static string EscapeGroovyString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    public async Task EnsureGitHubCredentialAsync(string username, string githubToken)
    {
        string credentialId = $"github-token-{username}";

        string credentialXml = $"""
        <com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl>
          <scope>GLOBAL</scope>
          <id>{credentialId}</id>
          <description>GitHub token for {username}</description>
          <username>{username}</username>
          <password>{githubToken}</password>
        </com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl>
        """;

        string updateUrl = $"credentials/store/system/domain/_/credential/{credentialId}/config.xml";
        string createUrl = "credentials/store/system/domain/_/createCredentials";

        using var updateContent = new StringContent(credentialXml, Encoding.UTF8, "application/xml");
        var response = await SendPostAsync(updateUrl, updateContent);

        if (!response.IsSuccessStatusCode)
        {
            using var createContent = new StringContent(credentialXml, Encoding.UTF8, "application/xml");
            var createResponse = await SendPostAsync(createUrl, createContent);
            createResponse.EnsureSuccessStatusCode();
        }
    }

    public async Task CreateIsolatedUserWorkspaceAsync(string username, string password, string email)
    {
        var groovyScript = $@"
import jenkins.model.Jenkins
import hudson.security.HudsonPrivateSecurityRealm
import com.cloudbees.hudson.plugins.folder.Folder
import hudson.security.ProjectMatrixAuthorizationStrategy
import hudson.model.Item

def jenkins = Jenkins.get()

def realm = jenkins.getSecurityRealm()
if (realm instanceof HudsonPrivateSecurityRealm) {{
    def existing = realm.getUser('{EscapeGroovyString(username)}')
    if (existing == null) {{
        def user = realm.createAccount('{EscapeGroovyString(username)}', '{EscapeGroovyString(password)}')
        user.setFullName('{EscapeGroovyString(username)}')
        user.addProperty(new hudson.tasks.Mailer.UserProperty('{EscapeGroovyString(email)}'))
        user.save()
    }}}}

def folderName = '{EscapeGroovyString(username)}'
def folder = (Folder) jenkins.getItem(folderName)
if (folder == null) {{
    folder = jenkins.createProject(Folder.class, folderName)}}

def folderAuthClass = Class.forName('com.cloudbees.hudson.plugins.folder.properties.AuthorizationMatrixProperty')
def matrixProp = folder.getProperties().get(folderAuthClass)

if (matrixProp == null) {{
    def constructor = folderAuthClass.getDeclaredConstructor()
    constructor.setAccessible(true)
    matrixProp = constructor.newInstance()
    folder.addProperty(matrixProp)}}

def permissions = [Item.READ, Item.CREATE, Item.CONFIGURE, Item.BUILD, Item.CANCEL, Item.WORKSPACE, Item.DELETE]

permissions.each {{ perm -> matrixProp.add(perm, '{EscapeGroovyString(username)}')}}

if (jenkins.getAuthorizationStrategy() instanceof ProjectMatrixAuthorizationStrategy) {{
    permissions.each {{ perm -> matrixProp.add(perm, '{EscapeGroovyString(_options.Username)}') }}
}}

try {{
    def nonInheriting = Class.forName('org.jenkinsci.plugins.matrixauth.inheritance.NonInheritingStrategy').getDeclaredConstructor().newInstance()
    matrixProp.setInheritanceStrategy(nonInheriting)
}} catch (Exception e) {{
    println ""Info: inheritanceStrategy non disponible""
}}

folder.save()
jenkins.save()
print 'SUCCESS'
";

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "script", groovyScript }
        });

        var response = await SendPostAsync("scriptText", content);
        var resultText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode || !resultText.Contains("SUCCESS"))
        {
            throw new InvalidOperationException($"Erreur espace Jenkins : {resultText}");
        }
    }

    public async Task CreateJenkinsJobAsync(string username, string jobName, string gitUrl, string branch, string githubToken, string jenkinsfileContent)
    {
        // 1. Enregistrement sécurisé du token de l'utilisateur dans Jenkins
        await EnsureGitHubCredentialAsync(username, githubToken);

        string githubCredentialId = $"github-token-{username}";

        // 2. Remplacement des variables de ciblage Git manquantes dans le template
        jenkinsfileContent = jenkinsfileContent
            .Replace("{GIT_URL}", gitUrl)
            .Replace("{BRANCH}", branch)
            .Replace("{GITHUB_CREDENTIALS_ID}", githubCredentialId);

        // 3. Passage en Base64 pour l'injection Groovy
        string b64Jenkinsfile = ToBase64(jenkinsfileContent);

        var groovyScript = $@"
import jenkins.model.Jenkins
import com.cloudbees.hudson.plugins.folder.Folder
import org.jenkinsci.plugins.workflow.job.WorkflowJob
import org.jenkinsci.plugins.workflow.cps.CpsFlowDefinition
import java.nio.charset.StandardCharsets

def scriptContent = new String(Base64.getDecoder().decode('{b64Jenkinsfile}'), StandardCharsets.UTF_8)
def jenkins = Jenkins.get()
def folder = jenkins.getItem('{EscapeGroovyString(username)}')

if (folder == null) {{
    throw new Exception('Dossier utilisateur introuvable : ' + '{EscapeGroovyString(username)}')
}}

def job = folder.getItem('{EscapeGroovyString(jobName)}')
if (job == null) {{
    job = folder.createProject(WorkflowJob.class, '{EscapeGroovyString(jobName)}')
}}

// Remis à false car la directive complexe native 'GitSCM' du template requiert les droits hors-sandbox.
job.setDefinition(new CpsFlowDefinition(scriptContent, true))
job.save()

print 'SUCCESS'
";

        using var content = new StringContent(
            $"script={Uri.EscapeDataString(groovyScript)}",
            Encoding.UTF8,
            "application/x-www-form-urlencoded"
        );

        var response = await SendPostAsync("scriptText", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jenkins a rejeté la requête [{response.StatusCode}] : {responseBody}");
        }

        if (!responseBody.Contains("SUCCESS"))
        {
            throw new InvalidOperationException($"Le script Groovy a échoué côté Jenkins : {responseBody}");
        }
    }
}