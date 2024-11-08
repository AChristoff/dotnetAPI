using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using DotnetAPI.Conventions;
using DotnetAPI.Models;  // Make sure to include the namespace for EmailSettings
using DotnetAPI.Services; // Include the namespace for EmailService
using Microsoft.OpenApi.Models; // Add this line to include ParameterLocation

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Use environment variables
string? tokenKey = Environment.GetEnvironmentVariable("TokenKey");
string? PasswordKey = Environment.GetEnvironmentVariable("PasswordKey");

// CORS
string? devURL = Environment.GetEnvironmentVariable("CorsDevURL");
string? prodURL = Environment.GetEnvironmentVariable("CorsProdURL");

// Email
var environment = builder.Environment.IsProduction() ? "_PROD" : "";
string? smtpHost = Environment.GetEnvironmentVariable($"MAIL_HOST{environment}");
int smtpPort = int.Parse(Environment.GetEnvironmentVariable($"MAIL_PORT{environment}") ?? "465");
string? smtpSender = Environment.GetEnvironmentVariable($"MAIL_SENDER{environment}");
string? smtpAddress = Environment.GetEnvironmentVariable($"MAIL_ADDRESS{environment}");
string? smtpPassword = Environment.GetEnvironmentVariable($"MAIL_PASSWORD{environment}");

// Add error if environment variables are not set
if (string.IsNullOrEmpty(devURL) ||
    string.IsNullOrEmpty(prodURL) ||
    string.IsNullOrEmpty(tokenKey) ||
    string.IsNullOrEmpty(PasswordKey) ||
    string.IsNullOrEmpty(smtpHost) ||
    string.IsNullOrEmpty(smtpSender) ||
    string.IsNullOrEmpty(smtpAddress) ||
    string.IsNullOrEmpty(smtpPassword))
{
    Console.WriteLine("Environment variables are not set. Please check the .env file.");
    return;
}

// Configure EmailSettings
builder.Services.Configure<EmailSettings>(options =>
{
    options.SmtpServer = smtpHost;
    options.Port = smtpPort;
    options.SenderName = smtpSender;
    options.SenderEmail = smtpAddress;
    options.Username = smtpAddress;  // Username is typically the email address for SMTP login
    options.Password = smtpPassword;
});
// Register EmailService
builder.Services.AddTransient<EmailService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Your API", Version = "v1" });

    // Define JWT authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http, // Set to Http to treat it as HTTP authentication
        Scheme = "Bearer" // This will automatically add "Bearer " prefix in the authorization header
    });

    // Add JWT security requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "Bearer", // Must match the Scheme in the definition above
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add global route prefix "/api"
    options.Conventions.Add(new GlobalRoutePrefixConvention("api"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev_CORS", corsBuilder =>
    {
        corsBuilder.WithOrigins(devURL)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    options.AddPolicy("Prod_CORS", corsBuilder =>
    {
        corsBuilder.WithOrigins(prodURL)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// Build the API
var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseCors("Dev_CORS");
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseCors("Prod_CORS");
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
