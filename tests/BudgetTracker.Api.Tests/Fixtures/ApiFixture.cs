using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BudgetTracker.Api.Tests.Fixtures;

public class ApiFixture : WebApplicationFactory<IApiAssemblyMarker>, IAsyncLifetime
{
    private const string TestJwtSecret = "this-is-a-test-secret-key-for-jwt-tokens-that-is-long-enough";
    private const string TestJwtIssuer = "BudgetTrackerTests";
    private const string TestJwtAudience = "BudgetTrackerTestsAudience";

    private PostgreSqlContainer _postgreSqlContainer = null!;
    private string ConnectionString => _postgreSqlContainer.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg17")
            .WithDatabase("budget_tracker_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgreSqlContainer.StartAsync();

        await EnsureDatabaseCreatedAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_postgreSqlContainer != null)
        {
            await _postgreSqlContainer.DisposeAsync();
        }
        await base.DisposeAsync();
    }


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDbContextFactory<BudgetTrackerContext>();

            services.RemoveAll(typeof(DbContextOptions<BudgetTrackerContext>));
            services.RemoveAll(typeof(BudgetTrackerContext));

            services.AddDbContext<BudgetTrackerContext>(options =>
                options.UseNpgsql(ConnectionString, o => o.UseVector()));

            // Override authorization policy to use only JWT Bearer for tests
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // Add JWT authentication for testing
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = TestJwtIssuer,
                        ValidAudience = TestJwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret))
                    };
                });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();

            context.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
        builder.UseSetting("https_port", "");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        client.BaseAddress = new Uri("https://localhost");
        base.ConfigureClient(client);
    }

    public BudgetTrackerContext CreateBudgetTrackerDbContext()
    {
        var db = Services.GetRequiredService<IDbContextFactory<BudgetTrackerContext>>().CreateDbContext();
        db.Database.EnsureCreated();
        return db;
    }

    public string GenerateJwtToken(string userId, string email)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(TestJwtSecret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, email)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = TestJwtIssuer,
            Audience = TestJwtAudience,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public void AuthenticateClient(HttpClient client, string userId, string email)
    {
        var token = GenerateJwtToken(userId, email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<ApplicationUser> CreateTestUserAsync(string email = "test@example.com",
        string password = "Test123!")
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Check if user already exists
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return existingUser;
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email = "test@example.com",
        string password = "Test123!")
    {
        var client = CreateClient();
        var user = await CreateTestUserAsync(email, password);
        AuthenticateClient(client, user.Id, user.Email!);
        return client;
    }

    public async Task LoginAsync(HttpClient client, string email = "test@example.com", string password = "Test123!")
    {
        var user = await CreateTestUserAsync(email, password);
        AuthenticateClient(client, user.Id, user.Email!);
    }




    private async Task EnsureDatabaseCreatedAsync()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerContext>()
            .UseNpgsql(ConnectionString, o => o.UseVector())
            .Options;

        await using var context = new BudgetTrackerContext(options);
        await context.Database.MigrateAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<ApiFixture>
{
}
