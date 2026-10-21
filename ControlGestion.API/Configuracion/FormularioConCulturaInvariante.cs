using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ControlGestion.API.Configuracion;

/// <summary>
/// Lee los campos de un formulario (multipart, como el de una foto) con la cultura invariante, igual que el JSON y el
/// query string. ASP.NET usa la cultura del servidor para los formularios: en un servidor en español, la latitud
/// "-17.78" que manda la app se leería como -1778.
/// </summary>
public class FormularioConCulturaInvariante : IValueProviderFactory
{
    public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        var peticion = context.ActionContext.HttpContext.Request;
        if (!peticion.HasFormContentType) return;

        IFormCollection formulario;
        try
        {
            formulario = await peticion.ReadFormAsync();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            // Lo mismo que hace FormValueProviderFactory: el error queda en el campo y la respuesta es un 400.
            throw new ValueProviderException("No se pudo leer el formulario.", ex);
        }
        context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, formulario, CultureInfo.InvariantCulture));
    }
}
