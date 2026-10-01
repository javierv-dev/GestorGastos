using System.Text.Json.Serialization;
using GestorGastos.Api.Endpoints;
using GestorGastos.Application;
using GestorGastos.Domain.Transacciones;
using GestorGastos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplication(builder.Configuration.GetValue("Negocio:UmbralSaldoBajo", PoliticaDeUmbralFijo.UmbralPorDefecto));
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default") ?? "Data Source=gastos.db");
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
app.MapPresupuestosEndpoints();

app.Run();

// Expone Program a las pruebas de integración (WebApplicationFactory<Program>).
public partial class Program;
