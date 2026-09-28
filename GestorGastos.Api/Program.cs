using System.Text.Json.Serialization;
using GestorGastos.Api.Data;
using GestorGastos.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<GestorGastosDbContext>(opt => opt.UseSqlite("Data Source=gastos.db"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "GestorGastos.Api v1");
    });
}

app.UseHttpsRedirection();

app.MapTransaccionesEndpoints();

app.Run();
