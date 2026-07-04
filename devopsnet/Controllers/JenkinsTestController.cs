using devopsnet.Dto;
using devopsnet.Services;
using devopsnet.Services.archive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace devopsnet.Controllers;

[ApiController]
[Authorize]
[Route("api/jenkins")]
public class JenkinsTestController : ControllerBase
{
    private const string TestJenkinsfile = @"
pipeline {
    agent any
    parameters {
        string(name: 'REPO_URL', description: 'URL du depot Git')
        string(name: 'BRANCH', defaultValue: 'main', description: 'Branche')
        string(name: 'IMAGE_NAME', description: 'Nom image Docker')
        password(name: 'GITHUB_TOKEN', defaultValue: '', description: 'Token GitHub')
    }
    stages {
        stage('1. Nettoyage & Git Clone') {
            steps {
                cleanWs()
                script {
                    def cloneUrl = params.REPO_URL
                    def token = params.GITHUB_TOKEN.toString()
                    if (token?.trim()) {
                        cloneUrl = params.REPO_URL.replace('https://', ""https://${token}@"")
                    }
                    git url: cloneUrl, branch: ""${params.BRANCH}""
                }
            }
        }
        stage('2. Build de image Docker') {
            steps {
                sh ""docker build -t 192.168.196.3:8111/${params.IMAGE_NAME} .""
            }
        }
        stage('3. Push vers Nexus') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'nexus-credentials', usernameVariable: 'NEXUS_USER', passwordVariable: 'NEXUS_PASS')]) {
                    sh ""docker login -u $NEXUS_USER -p $NEXUS_PASS 192.168.196.3:8111""
                    sh ""docker push 192.168.196.3:8111/${params.IMAGE_NAME}""
                    sh ""docker logout 192.168.196.3:8111""
                }
            }
        }
    }
}
";

    private readonly JenkinsService _jenkinsService;
    private readonly GitHubAuthService _gitHubAuthService;

    public JenkinsTestController(JenkinsService jenkinsService, GitHubAuthService gitHubAuthService)
    {
        _jenkinsService = jenkinsService;
        _gitHubAuthService = gitHubAuthService;
    }

    [HttpPost("test-trigger")]
    public async Task<IActionResult> TestTrigger([FromBody] JenkinsTestTriggerDto dto)
    {
        var userId = GetCurrentUserId();

        try
        {
            var githubToken = await _gitHubAuthService.GetDecryptedTokenAsync(userId);
            await _jenkinsService.TriggerBuildAsync("deploy-test-v2", dto.RepoUrl, dto.Branch, dto.ImageName, githubToken);
            return Ok(new { message = "Build Jenkins déclenché avec succès." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }

    [HttpPost("test-create-user")]
    public async Task<IActionResult> TestCreateUser([FromBody] JenkinsTestTriggerDto dto)
    {
        try
        {
            await _jenkinsService.CreateUserAsync("test-user-csharp", "TestPassword123", "Test User", "test@example.com");
            return Ok(new { message = "Utilisateur Jenkins créé avec succès." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-create-job")]
    public async Task<IActionResult> TestCreateJob()
    {
        try
        {
            await _jenkinsService.CreateOrUpdatePipelineJobAsync("test-dynamic-job", TestJenkinsfile);
            return Ok(new { message = "Job Jenkins créé avec succès." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-dynamic-clone")]
    public async Task<IActionResult> TestDynamicClone([FromBody] DynamicCloneRequestDto dto)
    {
        var userId = GetCurrentUserId();

        try
        {
            // 1. Récupération sécurisée du token depuis PostgreSQL
            var githubToken = await _gitHubAuthService.GetDecryptedTokenAsync(userId);
            var jobName = "test-clone-dynamique";

            // 2. Injection à la volée du token dans le conteneur de credentials de Jenkins
            await _jenkinsService.CreateOrUpdateGitHubCredentialAsync(githubToken);

            // 3. Demande à Jenkins de générer un job SCM basé sur le Jenkinsfile de ce dépôt
            await _jenkinsService.CreateCloneJobForRepoAsync(jobName, dto.RepoUrl, dto.Branch);

            // 4. Déclenchement du build SCM
            await _jenkinsService.TriggerCloneJobAsync(jobName, githubToken);

            return Ok(new
            {
                message = "Job Jenkins SCM généré et déclenché avec succès !",
                repo = dto.RepoUrl,
                jobName = jobName
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}