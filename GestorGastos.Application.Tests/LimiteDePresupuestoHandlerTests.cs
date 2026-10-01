using GestorGastos.Application.Tests.Fakes;
using GestorGastos.Application.Transacciones.Actualizar;
using GestorGastos.Application.Transacciones.Crear;
using GestorGastos.Domain.Common;
using GestorGastos.Domain.Presupuestos;
using GestorGastos.Domain.Transacciones;

namespace GestorGastos.Application.Tests;

// Cómo los handlers de transacciones aplican la regla del presupuesto mensual (servicio de dominio + orquestador).
public class LimiteDePresupuestoHandlerTests
{
    private static readonly DateTime Septiembre = new(2026, 9, 15);
    private static readonly DateTime Agosto = new(2026, 8, 15);

    private readonly FakeTransaccionRepository _transacciones = new();
    private readonly FakePresupuestoRepository _presupuestos = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CrearTransaccionHandler HandlerCrear() =>
        new(_transacciones, ComprobadorFactory.Crear(_transacciones, _presupuestos), new PoliticaDeUmbralFijo(), _unitOfWork);

    private ActualizarTransaccionHandler HandlerActualizar() =>
        new(_transacciones, ComprobadorFactory.Crear(_transacciones, _presupuestos), _unitOfWork);

    private void PresupuestoDe(CategoriaTransaccion categoria, decimal limite) =>
        _presupuestos.Items.Add(Presupuesto.Crear(categoria, limite).Value);

    private Transaccion Sembrar(
        decimal monto,
        CategoriaTransaccion categoria = CategoriaTransaccion.Alimentacion,
        TipoTransaccion tipo = TipoTransaccion.Egreso,
        DateTime? fecha = null
    )
    {
        var transaccion = Transaccion.Crear("Previa", monto, tipo, categoria, fecha ?? Septiembre).Value;
        _transacciones.Items.Add(transaccion);
        return transaccion;
    }

    private Task<Result<CrearTransaccionResultado>> Crear(
        decimal monto,
        CategoriaTransaccion categoria = CategoriaTransaccion.Alimentacion,
        TipoTransaccion tipo = TipoTransaccion.Egreso,
        DateTime? fecha = null
    ) => HandlerCrear().Handle(new CrearTransaccionCommand("Nueva", monto, tipo, categoria, fecha ?? Septiembre), CancellationToken.None);

    private Task<Result> Actualizar(
        Transaccion transaccion,
        decimal monto,
        CategoriaTransaccion? categoria = null,
        TipoTransaccion tipo = TipoTransaccion.Egreso
    ) =>
        HandlerActualizar()
            .Handle(
                new ActualizarTransaccionCommand(
                    transaccion.Id,
                    "Editada",
                    monto,
                    tipo,
                    categoria ?? transaccion.Categoria,
                    transaccion.Fecha
                ),
                CancellationToken.None
            );

    // ---------- Crear ----------

    [Fact]
    public async Task Crear_SinPresupuesto_AceptaCualquierEgreso()
    {
        var resultado = await Crear(999_999m);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_DentroDelLimite_LoAceptaYLoGuarda()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(60m);

        var resultado = await Crear(30m);

        Assert.True(resultado.IsSuccess);
        Assert.Single(_transacciones.Agregadas);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_QueExcedeElPresupuesto_DevuelveConflictoSinAgregarNiGuardar()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(60m);

        var resultado = await Crear(50m);

        Assert.Equal(PresupuestoErrors.Excedido(CategoriaTransaccion.Alimentacion, 100m, 60m), resultado.Error);
        Assert.Empty(_transacciones.Agregadas);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Crear_QueLlegaExactoAlLimite_SeAcepta()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(60m);

        Assert.True((await Crear(40m)).IsSuccess);
    }

    [Fact]
    public async Task Crear_EnOtraCategoria_NoLoAfectaElPresupuestoDeLaPrimera()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(100m);

        Assert.True((await Crear(500m, CategoriaTransaccion.Transporte)).IsSuccess);
    }

    [Fact]
    public async Task Crear_EnOtroMes_NoCuentaLoGastadoEnElMesAnterior()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(100m, fecha: Agosto);

        Assert.True((await Crear(100m, fecha: Septiembre)).IsSuccess);
    }

    [Fact]
    public async Task Crear_UnIngresoEnLaCategoria_NoConsumeNiSeRechaza()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(100m);

        var ingreso = await Crear(5_000m, tipo: TipoTransaccion.Ingreso);
        var egresoDespues = await Crear(1m);

        Assert.True(ingreso.IsSuccess);
        Assert.Equal(PresupuestoErrors.Excedido(CategoriaTransaccion.Alimentacion, 100m, 100m), egresoDespues.Error);
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_ReportaElErrorDeValidacionAntesQueElDePresupuesto()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);

        var resultado = await Crear(0m);

        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
    }

    // ---------- Actualizar ----------

    [Fact]
    public async Task Actualizar_AumentarUnEgresoPropioHastaElLimite_SeAceptaPorqueNoSeCuentaASiMisma()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        var propia = Sembrar(60m);

        var resultado = await Actualizar(propia, 100m);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(100m, propia.Monto);
        Assert.Equal(1, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Actualizar_QueHaceExcederElPresupuesto_DevuelveConflictoYNoModificaLaEntidad()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        Sembrar(60m);
        var otra = Sembrar(30m);

        var resultado = await Actualizar(otra, 50m);

        Assert.Equal(PresupuestoErrors.Excedido(CategoriaTransaccion.Alimentacion, 100m, 60m), resultado.Error);
        Assert.Equal(30m, otra.Monto);
        Assert.Equal("Previa", otra.Descripcion);
        Assert.Equal(0, _unitOfWork.VecesGuardado);
    }

    [Fact]
    public async Task Actualizar_CambiandoALaCategoriaDeUnPresupuestoAgotado_SeRechaza()
    {
        PresupuestoDe(CategoriaTransaccion.Transporte, 50m);
        Sembrar(50m, CategoriaTransaccion.Transporte);
        var enAlimentacion = Sembrar(10m, CategoriaTransaccion.Alimentacion);

        var resultado = await Actualizar(enAlimentacion, 10m, CategoriaTransaccion.Transporte);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TipoError.Conflicto, resultado.Error.Tipo);
        Assert.Equal(CategoriaTransaccion.Alimentacion, enAlimentacion.Categoria);
    }

    [Fact]
    public async Task Actualizar_ConvertirUnIngresoEnUnEgresoQueExcede_SeRechaza()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        var ingreso = Sembrar(500m, tipo: TipoTransaccion.Ingreso);

        var resultado = await Actualizar(ingreso, 500m, tipo: TipoTransaccion.Egreso);

        Assert.True(resultado.IsFailure);
        Assert.Equal(TipoError.Conflicto, resultado.Error.Tipo);
        Assert.Equal(TipoTransaccion.Ingreso, ingreso.Tipo);
    }

    [Fact]
    public async Task Actualizar_ConDatosInvalidos_ReportaLaValidacionAntesQueElPresupuesto()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 100m);
        var propia = Sembrar(60m);

        var resultado = await Actualizar(propia, -1m);

        Assert.Equal(TransaccionErrors.MontoInvalido, resultado.Error);
        Assert.Equal(60m, propia.Monto);
    }

    [Fact]
    public async Task Actualizar_SiLaTransaccionNoExiste_DevuelveNoEncontradaAntesDeConsultarPresupuestos()
    {
        PresupuestoDe(CategoriaTransaccion.Alimentacion, 1m);
        var inexistente = Transaccion.Crear("X", 999m, TipoTransaccion.Egreso, CategoriaTransaccion.Alimentacion, Septiembre).Value;

        var resultado = await Actualizar(inexistente, 999m);

        Assert.Equal(TransaccionErrors.NoEncontrada, resultado.Error);
    }
}
