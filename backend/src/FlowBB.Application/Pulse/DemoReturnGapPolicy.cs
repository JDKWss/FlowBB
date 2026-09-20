using System.Globalization;
using FlowBB.Domain.Common;

namespace FlowBB.Application.Pulse;

public enum PulseAlertCode
{
    ReturnGap
}

public enum PulseAlertSeverity
{
    Warning
}

/// <summary>Alert PULSE (odpowiada schematowi <c>PulseAlert</c> z OpenAPI). Zawiera wylacznie agregat.</summary>
public sealed record PulseAlert(PulseAlertCode Code, PulseAlertSeverity Severity, string Message);

/// <summary>
/// Regula luki powrotowej dla agregatu PULSE i dla planerow bez rozkladu (docs/code/MODULE_PULSE.md). Planer trasy
/// z rozkladu MZK (<c>PlannerSource.MzkTimetable</c>) wyznacza luke z prawdziwych godzin i nie podlega tej regule.
/// Uczestnik z <see cref="TransportMode.PublicTransport"/>
/// nie ma dogodnego powrotu, gdy wydarzenie konczy sie o 22:00 lub pozniej czasu lokalnego <c>Europe/Warsaw</c>;
/// brak <c>EndAt</c> oznacza brak luki. To symulacja demonstracyjna, bez danych rozkladowych MZK.
/// Z tej klasy korzystaja PULSE, komunikat <c>PulseUpdated</c> i trasa planerow bez rozkladu, zeby ich liczby byly spojne.
/// </summary>
public static class DemoReturnGapPolicy
{
    public static readonly TimeOnly LateEndThreshold = new(22, 0);

    private static readonly TimeZoneInfo DemoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static bool IsLateEvent(DateTimeOffset? endAt)
    {
        if (endAt is null)
        {
            return false;
        }

        var localEnd = TimeZoneInfo.ConvertTime(endAt.Value, DemoTimeZone);
        return TimeOnly.FromDateTime(localEnd.DateTime) >= LateEndThreshold;
    }

    public static int CountParticipantsWithoutReturn(DateTimeOffset? endAt, ModalSplit modalSplit)
    {
        ArgumentNullException.ThrowIfNull(modalSplit);
        return IsLateEvent(endAt) ? modalSplit.PublicTransport : 0;
    }

    public static bool HasReturnGap(DateTimeOffset? endAt, TransportMode mode) =>
        mode == TransportMode.PublicTransport && IsLateEvent(endAt);

    /// <returns>Alert <see cref="PulseAlertCode.ReturnGap"/> albo <c>null</c>, gdy nikt nie ma luki.</returns>
    public static PulseAlert? CreateAlert(int participantsWithoutReturn)
    {
        if (participantsWithoutReturn <= 0)
        {
            return null;
        }

        var hour = LateEndThreshold.ToString("HH:mm", CultureInfo.InvariantCulture);
        return new PulseAlert(
            PulseAlertCode.ReturnGap,
            PulseAlertSeverity.Warning,
            $"{participantsWithoutReturn} {PeoplePhrase(participantsWithoutReturn)} dogodnego powrotu po {hour}.");
    }

    // Polska odmiana: 1 osoba nie ma, 2-4 osoby nie maja, pozostale (w tym 12-14 i 21) osob nie ma.
    private static string PeoplePhrase(int count)
    {
        var lastDigit = count % 10;
        var lastTwoDigits = count % 100;
        if (count == 1)
        {
            return "osoba nie ma";
        }

        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? "osoby nie maja" : "osob nie ma";
    }
}
