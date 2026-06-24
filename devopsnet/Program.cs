using devopsnet.Data;
using devopsnet.Options;
using devopsnet.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// --- Configuration : injecte les variables d'environnement chargées depuis .env ---
builder.Configuration["ConnectionStrings:Postgres"] = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
builder.Configuration["Jwt:SecretKey"] = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
builder.Configuration["Jwt:ExpirationMinutes"] = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES");
builder.Configuration["GitHub:ClientId"] = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID");
builder.Configuration["GitHub:ClientSecret"] = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET");
builder.Configuration["GitHub:CallbackUrl"] = Environment.GetEnvironmentVariable("GITHUB_CALLBACK_URL");
builder.Configuration["Cors:AllowedOrigin"] = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGIN");

// --- Base de données ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration["ConnectionStrings:Postgres"]));

// --- Options fortement typées ---
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection(GitHubOptions.SectionName));

// --- Data Protection (chiffrement du token GitHub) ---
builder.Services.AddDataProtection();

// --- HttpClient pour les Services qui appellent l'API GitHub ---
builder.Services.AddHttpClient<GitHubAuthService>();
builder.Services.AddHttpClient<GitHubRepositoryService>();

// --- Services métier ---
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<GitCloneService>();

// --- Authentification JWT ---
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey manquant.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
    };

    // --- AJOUT : permet à GitHubAuthController.Login d'être atteint par une vraie
    // navigation de navigateur (window.location.href), qui ne peut pas envoyer
    // de header Authorization. Le token est alors lu depuis le query string.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// --- Controllers + OpenAPI ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- CORS pour le frontend React ---
builder.Services.AddReactCorsPolicy(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsServiceExtensions.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();