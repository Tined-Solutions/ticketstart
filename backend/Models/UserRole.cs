using System.Text.Json.Serialization;

namespace TicketeraOnline.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Organizador,
    Staff,
    Admin
}
