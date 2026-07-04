using devopsnet.Data;
using devopsnet.Dto;
using devopsnet.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace devopsnet.Tests.Services;

public class UserServiceTests
{
    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUser_WhenDataIsValid()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new UserService(context, null);
        var dto = new UserCreateDto
        {
            Username = "charlie",
            Email = "charlie@test.com",
            Password = "MotDePasse123!"
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("charlie", result.Username);
        Assert.Equal("charlie@test.com", result.Email);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenUsernameAlreadyTaken()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new UserService(context, null);

        await service.CreateAsync(new UserCreateDto
        {
            Username = "charlie",
            Email = "charlie@test.com",
            Password = "MotDePasse123!"
        });

        var duplicateDto = new UserCreateDto
        {
            Username = "charlie",
            Email = "autre@test.com",
            Password = "AutreMotDePasse!"
        };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(duplicateDto));
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenEmailAlreadyTaken()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new UserService(context, null);

        await service.CreateAsync(new UserCreateDto
        {
            Username = "charlie",
            Email = "charlie@test.com",
            Password = "MotDePasse123!"
        });

        var duplicateDto = new UserCreateDto
        {
            Username = "autreUsername",
            Email = "charlie@test.com",
            Password = "AutreMotDePasse!"
        };

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(duplicateDto));
    }

    [Fact]
    public async Task CreateAsync_ShouldHashPassword_NotStoreItInClearText()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var service = new UserService(context, null);
        var dto = new UserCreateDto
        {
            Username = "charlie",
            Email = "charlie@test.com",
            Password = "MotDePasse123!"
        };

        // Act
        await service.CreateAsync(dto);
        var storedUser = await context.Users.FirstAsync();

        // Assert
        Assert.NotEqual(dto.Password, storedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(dto.Password, storedUser.PasswordHash));
    }
}