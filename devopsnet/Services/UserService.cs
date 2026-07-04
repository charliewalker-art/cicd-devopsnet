using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Models;
using Microsoft.EntityFrameworkCore;

namespace devopsnet.Services;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly JenkinsManagerService _jenkinsManagerService; // Injection du nouveau service de production

    public UserService(AppDbContext context, JenkinsManagerService jenkinsManagerService)
    {
        _context = context;
        _jenkinsManagerService = jenkinsManagerService;
    }

    public async Task<UserResponseDto> CreateAsync(UserCreateDto dto)
    {
        var usernameTaken = await _context.Users.ByUsername(dto.Username).AnyAsync();
        if (usernameTaken)
            throw new InvalidOperationException("Ce nom d'utilisateur est déjà pris.");

        var emailTaken = await _context.Users.ByEmail(dto.Email).AnyAsync();
        if (emailTaken)
            throw new InvalidOperationException("Cet email est déjà utilisé.");

        // Ouverture d'une transaction PostgreSQL sécurisée
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
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

            // Appel transparent au gestionnaire Jenkins (avec mot de passe brut pour son chiffrement interne)
            await _jenkinsManagerService.CreateIsolatedUserWorkspaceAsync(
                dto.Username,
                dto.Password,
                dto.Email
            );

            // Validation finale de l'ensemble si tout s'est bien passé
            await transaction.CommitAsync();

            return MapToResponseDto(user);
        }
        catch (Exception ex)
        {
            // Annulation complète en base de données en cas de bug côté Jenkins
            await transaction.RollbackAsync();
            throw new InvalidOperationException($"Erreur lors de l'inscription. Synchronisation de l'infrastructure échouée : {ex.Message}");
        }
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