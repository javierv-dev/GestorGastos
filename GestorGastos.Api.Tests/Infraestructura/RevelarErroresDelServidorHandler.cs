namespace GestorGastos.Api.Tests.Infraestructura;

// Convierte cualquier respuesta 5xx en una excepción que incluye el cuerpo (en desarrollo trae la excepción del servidor).
// Sin esto, una prueba solo vería "InternalServerError" y no sabría por qué.
public sealed class RevelarErroresDelServidorHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var respuesta = await base.SendAsync(request, cancellationToken);

        if ((int)respuesta.StatusCode >= 500)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"El servidor respondió {(int)respuesta.StatusCode} a {request.Method} {request.RequestUri?.PathAndQuery}. Cuerpo: {cuerpo}"
            );
        }

        return respuesta;
    }
}
