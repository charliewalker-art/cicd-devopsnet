using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Models;
using devopsnet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace devopsnet.Tests.Services;

public class AuthServiceTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static TokenService CreateTokenService()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "cle-de-test-suffisamment-longue-pour-hmac-sha256",
            ["Jwt:ExpirationMinutes"] = "60",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new TokenService(configuration);
    }

    private static async Task<User> SeedUserAsync(AppDbContext context, string username, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = $"{username}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var tokenService = CreateTokenService();
        var service = new AuthService(context, tokenService);

        await SeedUserAsync(context, "charlie", "MotDePasse123!");

        var dto = new LoginRequestDto { Username = "charlie", Password = "MotDePasse123!" };

        // Act
        var result = await service.LoginAsync(dto);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("charlie", result.User.Username);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenUsernameDoesNotExist()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var tokenService = CreateTokenService();
        var service = new AuthService(context, tokenService);

        var dto = new LoginRequestDto { Username = "inexistant", Password = "MotDePasse123!" };

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_ShouldThrow_WhenPasswordIsIncorrect()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var tokenService = CreateTokenService();
        var service = new AuthService(context, tokenService);

        await SeedUserAsync(context, "charlie", "MotDePasse123!");

        var dto = new LoginRequestDto { Username = "charlie", Password = "MauvaisMotDePasse" };

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(dto));
    }
}