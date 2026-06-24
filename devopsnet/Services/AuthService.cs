using devopsnet.Data;
using devopsnet.Dto;
using Microsoft.EntityFrameworkCore;

namespace devopsnet.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthService(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var user = await _context.Users.ByUsername(dto.Username).FirstOrDefaultAsync();

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Identifiants invalides.");

        var (token, expiresAt) = _tokenService.GenerateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
            },
        };
    }
}