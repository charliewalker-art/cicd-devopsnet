using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Models;
using Microsoft.EntityFrameworkCore;

namespace devopsnet.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserResponseDto> CreateAsync(UserCreateDto dto)
    {
        var usernameTaken = await _context.Users.ByUsername(dto.Username).AnyAsync();
        if (usernameTaken)
            throw new InvalidOperationException("Ce nom d'utilisateur est déjà pris.");

        var emailTaken = await _context.Users.ByEmail(dto.Email).AnyAsync();
        if (emailTaken)
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow,
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return MapToResponseDto(user);
    }

    public async Task<User?> GetEntityByIdAsync(Guid id)
    {
        return await _context.Users.ById(id).FirstOrDefaultAsync();
    }

    private static UserResponseDto MapToResponseDto(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
        };
    }
}