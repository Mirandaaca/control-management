using System.Text.Json;
using System.Text.Json.Serialization;
using ControlGestion.API.Models;

namespace ControlGestion.API.Serialization;

/// <summary>
/// Red de seguridad: todo ApplicationUser que la API convierta a JSON sale solo como { id, nombreCompleto }.
/// Sin esto, las entidades con usuarios anidados exponían passwordHash, securityStamp y los demás campos de Identity.
/// </summary>
public class ApplicationUserJsonConverter : JsonConverter<ApplicationUser>
{
    // La API nunca acepta usuarios anidados en el body: el valor se descarta y queda en null.
    public override ApplicationUser? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, ApplicationUser value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("nombreCompleto", value.NombreCompleto);
        writer.WriteEndObject();
    }
}
