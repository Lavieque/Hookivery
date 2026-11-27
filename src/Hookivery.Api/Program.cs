using Hookivery.Api.ErrorHandling;
using Hookivery.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddErrorHandling();
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

await app.RunAsync();

