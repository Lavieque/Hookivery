using Hookivery.Api.ErrorHandling;
using Hookivery.Api.Sources;
using Hookivery.Application.Sources;
using Hookivery.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddErrorHandling();
builder.Services.AddValidation();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SourceService>();
builder.Services.AddOpenApi();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.UseErrorHandling();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapSourceEndpoints();

await app.RunAsync();

