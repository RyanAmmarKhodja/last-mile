using System.Text.Json.Serialization;
using LastMile.Messaging;
using LastMile.Tracking.Core;
using LastMile.Tracking.Data;
using LastMile.Tracking.Enums;
using LastMile.Tracking.Options;
using LastMile.Tracking.Services;
using LastMile.Tracking.Services.Background;
using LastMile.Tracking.Services.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection(TrackingOptions.Section));

var connectionString = builder.Configuration.GetConnectionString("TrackingDb")
    ?? throw new InvalidOperationException("Connection string 'TrackingDb' is not configured.");
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' is not configured.");

// ---- Data: PostgreSQL (read model + history) and Redis (live positions) ----------------------
builder.Services.AddDbContext<TrackingDbContext>(o => o.UseNpgsql(connectionString));
// abortConnect=false: start even if Redis is down; the multiplexer keeps reconnecting (NFR-3).
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

// ---- Application services ------------------------------------------------------------------
builder.Services.AddSingleton<LivePositionStore>();
builder.Services.AddSingleton<DriverContextCache>();
builder.Services.AddSingleton<PositionHub>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PositionHub>());
builder.Services.AddSingleton<RedisLock>();
builder.Services.AddScoped<TelemetryService>();
builder.Services.AddScoped<TrackingQueries>();
builder.Services.AddScoped<DriverEventHandler>();
builder.Services.AddScoped<ShipmentEventHandler>();

// ---- Background jobs: 2-minute sampling, live sweep, partition maintenance ---------------------
builder.Services.AddHostedService<PositionSampler>();
builder.Services.AddHostedService<LiveSweeper>();
builder.Services.AddHostedService<HistoryMaintenance>();

// ---- Messaging: consume fleet.driver.v1 + shipment.lifecycle.v1 (inbox). Publishes nothing. ----
builder.Services.AddKafkaMessaging(builder.Configuration, "tracking-service");
builder.Services.AddEventConsumer<TrackingEventsConsumer, TrackingDbContext>();

// ---- Web -----------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TrackingDbContext>("database")
    .AddCheck<RedisHealthCheck>("redis");
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---- Authentication: tokens issued by AuthService, keys from its OIDC metadata / JWKS ------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        o.MetadataAddress = jwt.Value.MetadataAddress;
        o.RequireHttpsMetadata = false; // internal docker network; TLS terminates at the edge
        o.MapInboundClaims = false;
        o.RefreshOnIssuerKeyNotFound = true; // AuthService key rotation
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudiences = jwt.Value.Audiences,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = TokenClaims.Subject,
            RoleClaimType = TokenClaims.Role
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Ingest, p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole(Roles.Driver) || ctx.User.CanIngestForAnyDriver()))
    .AddPolicy(Policies.TrackingReaders, p => p.RequireAssertion(ctx => ctx.User.CanReadTracking()))
    .AddPolicy(Policies.TrackingReadersOrDriver, p => p.RequireAssertion(ctx =>
        ctx.User.CanReadTracking() || ctx.User.IsInRole(Roles.Driver)));

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.Run();
