using System.Text.Json.Serialization;

namespace TicketeraOnline.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Organizador,
    Staff,
    Admin,
    // AUM-002 (D1): APPEND-ONLY — User.Role is int-stored with no value
    // conversion, so inserting or reordering members would corrupt existing
    // rows. Never insert a value before index 3 or renumber existing ones.
    SinAcceso
}
