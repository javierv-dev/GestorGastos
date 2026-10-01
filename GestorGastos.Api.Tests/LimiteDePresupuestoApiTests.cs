using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GestorGastos.Api.Tests.Infraestructura;

namespace GestorGastos.Api.Tests;

// El presupuesto mensual aplicado de punta a punta: API real, base SQLite y migraciones reales.
public class LimiteDePresupuestoApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Septiembre = "2026-09-15T10:30:00";
    private const string Agosto = "2026-08-15T10:30:00";

    private async Task<Guid> PresupuestoDe(string categoria, decimal limite)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/presupuestos", new { categoria, limiteMensual = limite }, Json);
        await EsperarEstado(respuesta, HttpStatusCode.Created);

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return documento.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> IntentarEgreso(decimal monto, string categoria = "Alimentacion", string fecha = Septiembre) =>
        Enviar(Cuerpo("Compra", monto, "Egreso", categoria, fecha));

    private Task<HttpResponseMessage> Actualizar(
        Guid id,
        decimal monto,
        string categoria = "Alimentacion",
        string tipo = "Egreso",
        string fecha = Septiembre
    ) => Cliente.PutAsJsonAsync($"/transacciones/{id}", Cuerpo("Editada", monto, tipo, categoria, fecha), Json);

    [Fact]
    public async Task SinPresupuesto_UnEgresoGrandeSeAcepta()
    {
        var respuesta = await IntentarEgreso(999_999m);

        await EsperarEstado(respuesta, HttpStatusCode.Created);
    }

    [Fact]
    public async Task EgresoDentroDelLimite_SeAcepta()
    {
        await PresupuestoDe("Alimentacion", 100m);
        await Crear("Previa", 60m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await IntentarEgreso(30m);

        await EsperarEstado(respuesta, HttpStatusCode.Created);
    }

    [Fact]
    public async Task EgresoQueExcede_Responde409ConElDetalleYNoSeGuarda()
    {
        await PresupuestoDe("Alimentacion", 100m);
        await Crear("Previa", 60m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await IntentarEgreso(50m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        using var problema = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("Presupuesto.Excedido", problema.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "El egreso excede el presupuesto mensual de Alimentacion: límite 100.00, ya gastado 60.00, disponible 40.00.",
            problema.RootElement.GetProperty("detail").GetString()
        );
        Assert.Single(await Listar());
        Assert.Equal(-60m, await Saldo());
    }

    [Fact]
    public async Task EgresoQueLlegaExactoAlLimite_SeAcepta()
    {
        await PresupuestoDe("Alimentacion", 100m);
        await Crear("Previa", 60m, "Egreso", "Alimentacion", Septiembre);

        Assert.Equal(HttpStatusCode.Created, (await IntentarEgreso(40m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(0.01m)).StatusCode);
    }

    [Fact]
    public async Task LosIngresosDeLaCategoria_NoConsumenPresupuestoNiSonRechazados()
    {
        await PresupuestoDe("Alimentacion", 100m);

        var ingreso = await Enviar(Cuerpo("Reembolso", 5_000m, "Ingreso", "Alimentacion", Septiembre));
        var egreso = await IntentarEgreso(100m);

        Assert.Equal(HttpStatusCode.Created, ingreso.StatusCode);
        Assert.Equal(HttpStatusCode.Created, egreso.StatusCode);
    }

    [Fact]
    public async Task LoGastadoEnOtroMes_NoCuentaParaElMesActual()
    {
        await PresupuestoDe("Alimentacion", 100m);
        await Crear("De agosto", 100m, "Egreso", "Alimentacion", Agosto);

        Assert.Equal(HttpStatusCode.Created, (await IntentarEgreso(100m, fecha: Septiembre)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(1m, fecha: Septiembre)).StatusCode);
    }

    [Fact]
    public async Task ElPresupuestoDeUnaCategoria_NoAfectaAOtra()
    {
        await PresupuestoDe("Alimentacion", 10m);

        var respuesta = await IntentarEgreso(500m, "Transporte");

        await EsperarEstado(respuesta, HttpStatusCode.Created);
    }

    [Fact]
    public async Task EliminarUnEgreso_LiberaPresupuesto()
    {
        await PresupuestoDe("Alimentacion", 100m);
        var previa = await Crear("Previa", 60m, "Egreso", "Alimentacion", Septiembre);
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(50m)).StatusCode);

        await Cliente.DeleteAsync($"/transacciones/{previa.Id}");

        Assert.Equal(HttpStatusCode.Created, (await IntentarEgreso(50m)).StatusCode);
    }

    [Fact]
    public async Task EliminarElPresupuesto_QuitaElLimite()
    {
        var id = await PresupuestoDe("Alimentacion", 100m);
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(500m)).StatusCode);

        await Cliente.DeleteAsync($"/presupuestos/{id}");

        Assert.Equal(HttpStatusCode.Created, (await IntentarEgreso(500m)).StatusCode);
    }

    [Fact]
    public async Task SubirElLimite_PermiteEgresosQueAntesSeRechazaban()
    {
        var id = await PresupuestoDe("Alimentacion", 100m);
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(150m)).StatusCode);

        await Cliente.PutAsJsonAsync($"/presupuestos/{id}", new { limiteMensual = 200m }, Json);

        Assert.Equal(HttpStatusCode.Created, (await IntentarEgreso(150m)).StatusCode);
    }

    [Fact]
    public async Task BajarElLimiteBajoLoYaGastado_NoEliminaNada_PeroBloqueaLosNuevosEgresos()
    {
        var id = await PresupuestoDe("Alimentacion", 500m);
        await Crear("Previa", 300m, "Egreso", "Alimentacion", Septiembre);

        var cambio = await Cliente.PutAsJsonAsync($"/presupuestos/{id}", new { limiteMensual = 100m }, Json);

        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        Assert.Single(await Listar());
        Assert.Equal(HttpStatusCode.Conflict, (await IntentarEgreso(1m)).StatusCode);
    }

    // ---------- Actualizar transacciones ----------

    [Fact]
    public async Task PutQueHaceExcederElPresupuesto_Responde409YNoModifica()
    {
        await PresupuestoDe("Alimentacion", 100m);
        await Crear("Una", 60m, "Egreso", "Alimentacion", Septiembre);
        var otra = await Crear("Otra", 30m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await Actualizar(otra.Id, 50m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var leida = (await Listar()).Single(t => t.Id == otra.Id);
        Assert.Equal(30m, leida.Monto);
        Assert.Equal("Otra", leida.Descripcion);
    }

    [Fact]
    public async Task PutDeUnEgresoPropioHastaElLimite_SeAcepta_PorqueNoSeCuentaASiMismo()
    {
        await PresupuestoDe("Alimentacion", 100m);
        var propia = await Crear("Propia", 60m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await Actualizar(propia.Id, 100m);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(100m, (await Listar()).Single().Monto);
    }

    [Fact]
    public async Task PutQueCambiaLaCategoriaAUnaConPresupuestoAgotado_Responde409()
    {
        await PresupuestoDe("Transporte", 50m);
        await Crear("Pasajes", 50m, "Egreso", "Transporte", Septiembre);
        var comida = await Crear("Comida", 10m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await Actualizar(comida.Id, 10m, categoria: "Transporte");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Alimentacion", (await Listar()).Single(t => t.Id == comida.Id).Categoria.ToString());
    }

    [Fact]
    public async Task PutConDatosInvalidos_Responde400_AunqueHayaPresupuesto()
    {
        await PresupuestoDe("Alimentacion", 100m);
        var propia = await Crear("Propia", 60m, "Egreso", "Alimentacion", Septiembre);

        var respuesta = await Actualizar(propia.Id, -1m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task PutDeUnaTransaccionInexistente_Responde404_AunqueExcedaElPresupuesto()
    {
        await PresupuestoDe("Alimentacion", 1m);

        var respuesta = await Actualizar(Guid.NewGuid(), 999m);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
