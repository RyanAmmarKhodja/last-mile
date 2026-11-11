using System.Text.Json.Serialization;
using LastMile.Messaging;
using LastMile.Shipments.Core;
using LastMile.Shipments.Data;
using LastMile.Shipments.Enums;
using LastMile.Shipments.Options;
using LastMile.Shipments.Services;
using LastMile.Shipments.Services.Clients;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<ServiceEndpointsOptions>(builder.Configuration.GetSection(ServiceEndpointsOptions.Section));

var connectionString = builder.Configuration.GetConnectionString("ShipmentDb")
    ?? throw new InvalidOperationException("Connection string 'ShipmentDb' is not configured.");

// ---- Data ----------------------------------------------------------------------------------
builder.Services.AddDbContext<ShipmentDbContext>(o => o.UseNpgsql(connectionString));

// ---- Application services ------------------------------------------------------------------
builder.Services.AddScoped<ShipmentEventPublisher>();
builder.Services.AddScoped<ShipmentService>();
builder.Services.AddScoped<DispatchService>();

// ---- Downstream services (M2M client credentials, tokens cached per audience) -----------------------
var endpoints = builder.Configuration.GetSection(ServiceEndpointsOptions.Section).Get<ServiceEndpointsOptions>() ?? new();
var timeout = TimeSpan.FromSeconds(Math.Max(1, endpoints.TimeoutSeconds));
const string TokenClient = "m2m-token";
builder.Services.AddHttpClient(TokenClient, c => { c.BaseAddress = new Uri(AsBase(endpoints.AuthBaseUrl)); c.Timeout = timeout; });
// One provider for the whole app: it caches tokens per audience.
builder.Services.AddSingleton(sp => new M2MTokenProvider(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(TokenClient),
    sp.GetRequiredService<IOptions<ServiceEndpointsOptions>>(),
    sp.GetRequiredService<ILogger<M2MTokenProvider>>()));
builder.Services.AddHttpClient<AuthClient>(c => { c.BaseAddress = new Uri(AsBase(endpoints.AuthBaseUrl)); c.Timeout = timeout; })
    .AddHttpMessageHandler(sp => new M2MTokenHandler(sp.GetRequiredService<M2MTokenProvider>(), Audiences.Auth, Scopes.UsersLookup));
builder.Services.AddHttpClient<ProfileClient>(c => { c.BaseAddress = new Uri(AsBase(endpoints.ProfileBaseUrl)); c.Timeout = timeout; })
    .AddHttpMessageHandler(sp => new M2MTokenHandler(sp.GetRequiredService<M2MTokenProvider>(), Audiences.Profile, Scopes.ProfilesRead));
builder.Services.AddHttpClient<FleetClient>(c => { c.BaseAddress = new Uri(AsBase(endpoints.FleetBaseUrl)); c.Timeout = timeout; })
    .AddHttpMessageHandler(sp => new M2MTokenHandler(sp.GetRequiredService<M2MTokenProvider>(), Audiences.Fleet, Scopes.FleetRead));

// ---- Messaging: publish shipment.lifecycle.v1 (outbox) ------------------------------------------
builder.Services.AddKafkaMessaging(builder.Configuration, "shipment-service");
builder.Services.AddOutbox<ShipmentDbContext>();

// ---- Web -----------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ShipmentDbContext>("database")
    .AddOutboxCheck<ShipmentDbContext>();
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
builder.Services.AddAuthorization();

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

// HttpClient.BaseAddress needs a trailing slash for relative paths ("api/v1/...").
static string AsBase(string url) => url.EndsWith('/') ? url : url + "/";
