using devopsnet.Dto;
using devopsnet.Services;
using Microsoft.AspNetCore.Mvc;

namespace devopsnet.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserService _userService;

    public AuthController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserCreateDto dto)
    {
        try
        {
            var result = await _userService.CreateAsync(dto);
            return CreatedAtAction(nameof(Register), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}