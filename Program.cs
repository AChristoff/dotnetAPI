using DotnetAPI.Conventions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new GlobalRoutePrefixConvention("api"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dev_CORS", corsBuilder =>
        {
            corsBuilder.WithOrigins("http://localhost:3000") // React dev server
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });


    options.AddPolicy("Prod_CORS", corsBuilder =>
        {
            corsBuilder.WithOrigins("https://costschef.com")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
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

app.UseAuthorization();
app.MapControllers();
app.Run();
