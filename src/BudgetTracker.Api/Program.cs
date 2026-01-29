using Azure.AI.OpenAI;
using BudgetTracker.Api.AntiForgery;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Analytics;
using BudgetTracker.Api.Features.Analytics.Insights;
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Budget Tracker API",
        Version = "v1",
        Description = "A minimal API for budget tracking with user authentication"
    });

    // Add API Key authentication
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints. Enter your API key in the text input below.",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Name = "X-API-Key",
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme"
    });

    // Make API Key required for all endpoints
    c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey", document)] = new List<string>()
    });
});

// Add Entity Framework with pgvector support
builder.Services.AddDbContext<BudgetTrackerContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));

// Add CSV Import Service
builder.Services.AddScoped<CsvImporter>();

// Add Image Import Service
builder.Services.AddScoped<IImageImporter, ImageImporter>();

// Add CSV Structure Detection Services
builder.Services.AddScoped<ICsvStructureDetector, CsvStructureDetector>();
builder.Services.AddScoped<ICsvDetector, CsvDetector>();
builder.Services.AddScoped<ICsvAnalyzer, CsvAnalyzer>();

// Add Auth with multiple schemes
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Identity.Application", "StaticApiKey")
        .RequireAuthenticatedUser()
        .Build();
});

// Add Identity
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<BudgetTrackerContext>();

// Configure Static API Keys
builder.Services.Configure<StaticApiKeysConfiguration>(
    builder.Configuration.GetSection(StaticApiKeysConfiguration.SectionName));

// Add Static API Key Authentication
builder.Services.AddAuthentication()
    .AddScheme<StaticApiKeyAuthenticationSchemeOptions, StaticApiKeyAuthenticationHandler>("StaticApiKey", options =>
    {
        var staticApiKeysConfig = builder.Configuration.GetSection(StaticApiKeysConfiguration.SectionName).Get<StaticApiKeysConfiguration>();
        if (staticApiKeysConfig?.Keys != null)
        {
            // Map each configured API key to its associated user ID
            foreach (var keyConfig in staticApiKeysConfig.Keys)
            {
                var apiKey = keyConfig.Key;
                var keyInfo = keyConfig.Value;
                options.ValidApiKeys[apiKey] = keyInfo.UserId;
            }
        }
    });

// Add Anti-forgery services
builder.Services.AddAntiforgery(options =>
{
    options.SuppressXFrameOptionsHeader = false;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDevelopment", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://localhost:3001") // TODO: Update with configurable origins
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Configure Azure AI
builder.Services.Configure<AzureAiConfiguration>(
    builder.Configuration.GetSection(AzureAiConfiguration.SectionName));

// Register IChatClient for Azure OpenAI
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AzureAiConfiguration>>().Value;

    if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.ApiKey))
    {
        throw new InvalidOperationException(
            "Azure AI configuration is missing. Please configure Endpoint and ApiKey in user secrets.");
    }

    return new AzureOpenAIClient(
        new Uri(config.Endpoint),
        new System.ClientModel.ApiKeyCredential(config.ApiKey))
        .GetChatClient(config.DeploymentName)
        .AsIChatClient();
});

// Register IEmbeddingGenerator using Microsoft.Extensions.AI
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AzureAiConfiguration>>().Value;
    return new AzureOpenAIClient(
        new Uri(config.Endpoint),
        new System.ClientModel.ApiKeyCredential(config.ApiKey))
        .GetEmbeddingClient(config.EmbeddingDeploymentName)
        .AsIEmbeddingGenerator();
});

// Register enhancement service
builder.Services.AddScoped<ITransactionEnhancer, TransactionEnhancer>();

// Register embedding services
builder.Services.AddScoped<IAzureEmbeddingService, AzureEmbeddingService>();
builder.Services.AddHostedService<EmbeddingBackgroundService>();

// Register semantic search and query assistant services
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IQueryAssistantService, QueryAssistantService>();

// Register analytics services
builder.Services.AddScoped<IInsightsService, AzureAiInsightsService>();

var app = builder.Build();

// Apply migrations at startup
// This is suitable for development but not recommended for production scenarios.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowLocalDevelopment");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map feature endpoints
app.MapGet("/", () => "API");
app
    .MapGroup("/api")
    .MapAntiForgeryEndpoints()
    .MapAuthEndpoints()
    .MapTransactionEndpoints()
    .MapQueryEndpoints()
    .MapAnalyticsEndpoints();

app.Run();
