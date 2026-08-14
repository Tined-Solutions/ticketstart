using System.Text.Json.Serialization;

namespace TicketeraOnline.Api.Models;

/// <summary>
/// Approval lifecycle of an event (EA-001). Stored as int in the DB
/// (mirrors <see cref="TransactionStatus"/>/<see cref="ReservationStatus"/>);
/// serialized as the member name string in JSON (mirrors <see cref="UserRole"/>,
/// the only frontend-consumed enum convention): "Pending"/"Approved"/"Rejected".
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventStatus
{
    Pending,
    Approved,
    Rejected
}
