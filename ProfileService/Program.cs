using System.Text.Json.Serialization;
using LastMile.Messaging;
using LastMile.Profile.Core;
using LastMile.Profile.Data;
using LastMile.Profile.Enums;
using LastMile.Profile.Options;
using LastMile.Profile.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));

var connectionString = builder.Configuration.GetConnectionString("ProfileDb")
    ?? throw new InvalidOperationException("Connection string 'ProfileDb' is not configured.");

// ---- Data ----------------------------------------------------------------------------------
builder.Services.AddDbContext<ProfileDbContext>(o => o.UseNpgsql(connectionString));

// ---- Application services ------------------------------------------------------------------
builder.Services.AddScoped<ClientEventPublisher>();
builder.Services.AddScoped<ClientProfileService>();
builder.Services.AddScoped<AdminProfileService>();
builder.Services.AddScoped<UserEventHandler>();

// ---- Messaging: consume auth.user.v1 (inbox), publish profile.client.v1 (outbox) ---------------
builder.Services.AddKafkaMessaging(builder.Configuration, "profile-service");
builder.Services.AddOutbox<ProfileDbContext>();
builder.Services.AddEventConsumer<AuthUserConsumer, ProfileDbContext>();

// ---- Web -----------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProfileDbContext>("database")
    .AddOutboxCheck<ProfileDbContext>();
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
    .AddPolicy(Policies.ClientReaders, p => p.RequireAssertion(ctx => ctx.User.CanReadAllClients()))
    .AddPolicy(Policies.ClientReadersOrOwner, p => p.RequireAssertion(ctx =>
        ctx.User.CanReadAllClients() || ctx.User.IsInRole(Roles.Client)));

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
