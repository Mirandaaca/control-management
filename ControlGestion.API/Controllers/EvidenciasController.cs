using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EvidenciasController(EvidenciaService evidencias) : ControllerBase
{
    // Una foto de un evento se abre con los permisos que abren el evento, y un comprobante, con los que abren la
    // cobranza. El servicio revisa de cuál es y aplica la cartera.
    [HttpGet("{id:int}/archivo")]
    [Permiso(Permisos.Eventos.ConsultarEventos, Permisos.Eventos.RegistrarEvento, Permisos.Eventos.ModificarEvento,
        Permisos.Eventos.ResolverEvento, Permisos.Eventos.AgregarFoto, Permisos.Eventos.EliminarFoto, Permisos.Eventos.VerMapa,
        Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos, Permisos.Cobranzas.RegistrarPago,
        Permisos.Cobranzas.AnularPago)]
    public async Task<IActionResult> GetArchivo(int id)
    {
        var (contenido, tipo) = await evidencias.AbrirAsync(id);
        // El tipo es el de la firma del archivo, y nosniff impide que el navegador lo adivine por su cuenta (T7.05).
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private";
        return File(contenido, tipo);
    }
}
