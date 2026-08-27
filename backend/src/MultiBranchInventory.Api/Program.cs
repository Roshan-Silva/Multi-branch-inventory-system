using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MultiBranchInventory.Application.Authentication.Interfaces;
using MultiBranchInventory.Infrastructure.Authentication;
using MultiBranchInventory.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// Database Configuration
// ======================================================

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// ======================================================
// JWT Configuration
// ======================================================

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration was not found.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException(
        "JWT signing key was not configured.");
}


// ======================================================
// Authentication Services
// ======================================================

builder.Services.AddScoped<IPasswordHasher, PasswordHasherService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();


// ======================================================
// JWT Authentication
// ======================================================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.Key)),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();


// ======================================================
// Initial SuperAdmin Seeder Configuration
// ======================================================

builder.Services.Configure<AdminSeedSettings>(
    builder.Configuration.GetSection(
        AdminSeedSettings.SectionName));

builder.Services.AddScoped<DatabaseSeeder>();


// ======================================================
// Controllers / OpenAPI
// ======================================================

builder.Services.AddControllers();

builder.Services.AddOpenApi();


// ======================================================
// Build Application
// ======================================================

var app = builder.Build();


// ======================================================
// Seed Initial Database Data
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider
        .GetRequiredService<DatabaseSeeder>();

    await seeder.SeedAsync();
}


// ======================================================
// HTTP Request Pipeline
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


// IMPORTANT:
// Authentication must come before Authorization.

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();


// ======================================================
// Run Application
// ======================================================

app.Run();