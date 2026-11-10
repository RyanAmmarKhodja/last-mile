using System.Text.Json.Serialization;
using LastMile.Messaging;
using LastMile.Fleet.Core;
using LastMile.Fleet.Data;
using LastMile.Fleet.Enums;
using LastMile.Fleet.Options;
using LastMile.Fleet.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));

var connectionString = builder.Configuration.GetConnectionString("FleetDb")
    ?? throw new InvalidOperationException("Connection string 'FleetDb' is not configured.");

// ---- Data ----------------------------------------------------------------------------------
builder.Services.AddDbContext<FleetDbContext>(o => o.UseNpgsql(connectionString));

// ---- Application services ------------------------------------------------------------------
builder.Services.AddScoped<FleetEventPublisher>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<UserEventHandler>();

// ---- Messaging: consume auth.user.v1 (inbox), publish fleet.driver.v1 / fleet.vehicle.v1 (outbox)
builder.Services.AddKafkaMessaging(builder.Configuration, "fleet-service");
builder.Services.AddOutbox<FleetDbContext>();
builder.Services.AddEventConsumer<AuthUserConsumer, FleetDbContext>();

// ---- Web -----------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FleetDbContext>("database")
    .AddOutboxCheck<FleetDbContext>();
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
    .AddPolicy(Policies.FleetReaders, p => p.RequireAssertion(ctx => ctx.User.CanReadFleet()))
    .AddPolicy(Policies.FleetReadersOrDriver, p => p.RequireAssertion(ctx =>
        ctx.User.CanReadFleet() || ctx.User.IsInRole(Roles.Driver)));

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
