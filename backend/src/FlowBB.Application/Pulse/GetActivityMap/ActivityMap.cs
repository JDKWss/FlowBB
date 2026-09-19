namespace FlowBB.Application.Pulse.GetActivityMap;

/// <summary>Zagregowana komorka siatki. Nie zawiera punktow uzytkownikow ani ich identyfikatorow.</summary>
public sealed record HexCell(string Id, int Participants, ModalSplit ModalSplit, IReadOnlyList<HexVertex> Vertices);

/// <summary>Wynik agregacji PULSE wydarzenia. Komorki z mniej niz 10 osobami sa pominiete.</summary>
public sealed record ActivityMap(int ParticipantsCount, ModalSplit ModalSplit, IReadOnlyList<HexCell> Cells);
