using FlowBB.Domain.Common;

namespace FlowBB.Application.Routing;

/// <summary>
/// Snapshot deklaracji "Ide" potrzebny do wyznaczenia trasy: punkt startu i wybrany srodek transportu.
/// Dane wewnetrzne backendu, wspolrzedne nigdy nie trafiaja do odpowiedzi API.
/// </summary>
public sealed record AttendanceOrigin(GeoPoint Origin, TransportMode Mode);
