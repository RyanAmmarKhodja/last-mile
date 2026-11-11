using System.Text.Json.Serialization;
using LastMile.Auth.Core;
using LastMile.Auth.Data;
using LastMile.Auth.Enums;
using LastMile.Auth.Options;
using LastMile.Auth.Services;
using LastMile.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration -------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));

var connectionString = builder.Configuration.GetConnectionString("AuthDb")
    ?? throw new InvalidOperationException("Connection string 'AuthDb' is not configured.");

// ---- Data ----------------------------------------------------------------------------------
builder.Services.AddDbContext<AuthDbContext>(o => o.UseNpgsql(connectionString));

// ---- Application services ------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SigningKeyService>();
builder.Services.AddSingleton<PasswordHashingService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ITokenBlacklist, TokenBlacklistService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ServiceClientService>();
builder.Services.AddHostedService<TokenCleanupService>();

// ---- Messaging: auth.user.v1 events through the transactional outbox ---------------------------
builder.Services.AddKafkaMessaging(builder.Configuration, "auth-service");
builder.Services.AddOutbox<AuthDbContext>();
builder.Services.AddScoped<UserEventPublisher>();

// ---- Web -----------------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AuthDbContext>("database")
    .AddOutboxCheck<AuthDbContext>();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Behind NGINX inside the docker network: trust X-Forwarded-* so audit logs record the real client IP.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---- Authentication: validate our own RS256 tokens + blacklist check ------------------------
// Two schemes with the same checks (signature, issuer, lifetime, jti blacklist):
//  - Bearer: AuthService's own endpoints, audiences last-mile-api / auth-service.
//  - Gateway: only GET /auth/validate (NGINX auth_request). It skips the audience check, because the gateway asks
//    "is this token genuine and not revoked?" for every service (e.g. M2M tokens for tracking-service); the
//    downstream service still enforces its own audience.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddJwtBearer(AuthSchemes.Gateway);
foreach (var scheme in new[] { JwtBearerDefaults.AuthenticationScheme, AuthSchemes.Gateway })
builder.Services.AddOptions<JwtBearerOptions>(scheme)
    .Configure<SigningKeyService, IOptions<JwtOptions>>((o, keys, jwt) =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudiences = [jwt.Value.Audience, jwt.Value.ServiceAudience],
            ValidateAudience = scheme != AuthSchemes.Gateway,
            ValidAlgorithms = [SigningKeyService.Algorithm],
            IssuerSigningKeyResolver = (_, _, kid, _) =>
                keys.ValidationKeys.Where(k => kid is null || k.KeyId == kid),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = TokenClaims.Subject,
            RoleClaimType = TokenClaims.Role
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var jti = ctx.Principal?.FindFirst(TokenClaims.JwtId)?.Value;
                if (jti is null)
                {
                    ctx.Fail("Token has no jti.");
                    return;
                }
                var blacklist = ctx.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                if (await blacklist.IsRevokedAsync(jti, ctx.HttpContext.RequestAborted))
                    ctx.Fail("Token has been revoked.");
            }
        };
    });
builder.Services.AddAuthorizationBuilder()
    // ADMIN users, or services with users:lookup (ShipmentService resolving a recipient by email, decision S1).
    .AddPolicy(Policies.UserLookup, p => p.RequireAssertion(ctx =>
        ctx.User.IsAdmin() || ctx.User.HasScope(Scopes.UsersLookup)));

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

// OIDC-style discovery so downstream ASP.NET services can point JwtBearer.MetadataAddress here.
app.MapGet("/.well-known/openid-configuration", (HttpRequest req, IOptions<JwtOptions> jwt) =>
{
    var baseUrl = $"{req.Scheme}://{req.Host}";
    return Results.Json(new Dictionary<string, object>
    {
        ["issuer"] = jwt.Value.Issuer,
        ["jwks_uri"] = $"{baseUrl}/api/v1/auth/jwks.json",
        ["token_endpoint"] = $"{baseUrl}/api/v1/auth/m2m/token",
        ["grant_types_supported"] = new[] { "password", "refresh_token", "client_credentials" },
        ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_basic", "client_secret_post" },
        ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
    });
});

app.Run();
