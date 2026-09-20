namespace FlowBB.Application.Pulse;

/// <summary>
/// Wewnetrzny snapshot wydarzenia i punktow PULSE pobrany jednym odczytem persystencji.
/// Punkty nie zawieraja identyfikatorow uzytkownikow i nie moga opuszczac backendu.
/// </summary>
public sealed record PulseEventSnapshot(
    PulseEventInfo Event,
    IReadOnlyList<PulsePoint> Points);
