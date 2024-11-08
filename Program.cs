using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using DotnetAPI.Conventions;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Use environment variables
string? devURL = Environment.GetEnvironmentVariable("CorsDevURL");
string? prodURL = Environment.GetEnvironmentVariable("CorsProdURL");
string? tokenKey = Environment.GetEnvironmentVariable("TokenKey");
string? PasswordKey = Environment.GetEnvironmentVariable("PasswordKey");

// Add error if environment variables are not set
if (string.IsNullOrEmpty(devURL) ||
    string.IsNullOrEmpty(prodURL) ||
    string.IsNullOrEmpty(tokenKey) ||
    string.IsNullOrEmpty(PasswordKey)
   )
{
    Console.WriteLine("Environment variables are not set. Please check the .env file.");
    return;
}

builder.Services.AddControllers(options =>
{
    // Add global route prefix "/api"
    options.Conventions.Add(new GlobalRoutePrefixConvention("api"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
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
