using GestorGastos.Application.Presupuestos;

namespace GestorGastos.Application.Tests.Fakes;

public static class ComprobadorFactory
{
    // Servicio de aplicación real con repositorios falsos; el servicio de dominio es estático y real.
    public static ComprobadorDePresupuesto Crear(FakeTransaccionRepository transacciones, FakePresupuestoRepository presupuestos) =>
        new(transacciones, presupuestos);
}
