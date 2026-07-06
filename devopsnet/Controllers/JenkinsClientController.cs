using System;
using System.Threading.Tasks;
using devopsnet.Services;
using Microsoft.AspNetCore.Mvc;

namespace devopsnet.Controllers;

[ApiController]
[Route("api/jenkins")]
public class JenkinsClientController : ControllerBase
{
    private readonly JenkinsQueryService _queryService;

    public JenkinsClientController(JenkinsQueryService queryService)
    {
        _queryService = queryService;
    }

    // GET: api/jenkins/pipelines/charliewalker
    [HttpGet("pipelines/{username}")]
    public async Task<IActionResult> GetPipelines(string username)
    {
        try
        {
            var data = await _queryService.GetUserPipelinesAsync(username);
            return Content(data, "application/json");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST: api/jenkins/pipelines/charliewalker/monsitetest/build
    [HttpPost("pipelines/{username}/{jobName}/build")]
    public async Task<IActionResult> BuildPipeline(string username, string jobName)
    {
        try
        {
            await _queryService.TriggerBuildAsync(username, jobName);
            return Ok(new { message = "Build démarré avec succès !" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // DELETE: api/jenkins/pipelines/charliewalker/monsitetest
    [HttpDelete("pipelines/{username}/{jobName}")]
    public async Task<IActionResult> DeletePipeline(string username, string jobName)
    {
        try
        {
            await _queryService.DeletePipelineAsync(username, jobName);
            return Ok(new { message = "Pipeline supprimé avec succès." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET: api/jenkins/pipelines/charliewalker/monsitetest/logs
    [HttpGet("pipelines/{username}/{jobName}/logs")]
    public async Task<IActionResult> GetLogs(string username, string jobName)
    {
        try
        {
            var logs = await _queryService.GetBuildLogsAsync(username, jobName);
            return Ok(new { logs = logs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}