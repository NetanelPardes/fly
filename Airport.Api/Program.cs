using Airport.Api.Data;
using Airport.Api.Middleware;
using Airport.Api.Models;
using Airport.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AirportDb")
    ?? builder.Configuration["MYSQL_CONNECTION_STRING"]
    ?? BuildMySqlConnectionString(builder.Configuration);

builder.Services.AddDbContext<AirportDbContext>(options =>
{
    var serverVersion = builder.Configuration["MYSQL_SERVER_VERSION"] ?? "8.0.36";
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(Version.Parse(serverVersion)),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<AirportLocationCatalog>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHostedService<TicketOutboxPublisher>();
builder.Services.AddHostedService<CsvFlightScheduleHostedService>();

var jwtKey = builder.Configuration["JWT_KEY"] ?? "development-only-secret-key-change-me-please-32chars";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "AirportManagementSystem";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "AirportManagementSystem";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AirportUi", policy =>
    {
        var origins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AirportUi");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health");

await DemoDataSeeder.SeedUsersAsync(app.Services);

app.Run();

static string BuildMySqlConnectionString(IConfiguration configuration)
{
    var host = configuration["MYSQL_HOST"] ?? "localhost";
    var port = configuration["MYSQL_PORT"] ?? "3306";
    var database = configuration["MYSQL_DATABASE"] ?? "airport_management";
    var user = configuration["MYSQL_USER"] ?? "airport_user";
    var password = configuration["MYSQL_PASSWORD"] ?? "airport_password";

    return $"Server={host};Port={port};Database={database};User={user};Password={password};TreatTinyAsBoolean=true;";
}
