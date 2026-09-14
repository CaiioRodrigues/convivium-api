using System.Text;
using System.Text.Json.Serialization;
using Convivium.Api.Auth;
using Convivium.Api.Common;
using Convivium.Application;
using Convivium.Application.Abstractions;
using Convivium.Application.Auth;
using Convivium.Domain.People;
using Convivium.Infrastructure;
using Convivium.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums viajam como texto: o front do convivium-web le "Manager",
        // nao 5, e um valor novo no meio do enum nao quebra o cliente.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// --- Autenticacao ---

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Secao Jwt ausente na configuracao.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sem o mapeamento legado, "sub" continua "sub" em vez de virar
        // a URI longa de nameidentifier do WS-Federation.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ConviviumClaims.Name,
            RoleClaimType = ConviviumClaims.Role,
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ConviviumPolicies.Member, policy =>
        policy.AddRequirements(new MinimumRoleRequirement(MembershipRole.Resident)))
    .AddPolicy(ConviviumPolicies.Council, policy =>
        policy.AddRequirements(new MinimumRoleRequirement(MembershipRole.CouncilMember)))
    .AddPolicy(ConviviumPolicies.Finance, policy =>
        policy.AddRequirements(new MinimumRoleRequirement(MembershipRole.AssistantManager)))
    .AddPolicy(ConviviumPolicies.Manager, policy =>
        policy.AddRequirements(new MinimumRoleRequirement(MembershipRole.Manager)));

// --- CORS para o convivium-web ---

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- Swagger ---

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Convivium API",
        Version = "v1",
        Description = "Gestao de condominios: caixa, rateio, cobrancas e prestacao de contas.",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole aqui o accessToken devolvido por POST /api/auth/login.",
    });

    // Swashbuckle 10 resolve a referencia contra o documento gerado,
    // por isso o requisito e uma funcao e nao um objeto pronto.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

await app.MigrateAndSeedAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Convivium API v1"));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposto para que os testes de integracao possam subir a API em memoria.</summary>
public partial class Program;
