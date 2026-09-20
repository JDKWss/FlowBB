namespace FlowBB.Domain.Routing;

/// <summary>
/// Zrodlo planu trasy. <see cref="Demo"/> to symulacja (<c>DemoRoutePlanner</c>), <see cref="RoadRouting"/> prawdziwa
/// trasa drogowa z prywatnej uslugi routingu, a <see cref="MzkTimetable"/> godziny odjazdow z rozkladu MZK.
/// <see cref="OpenTripPlanner"/> jest zarezerwowane dla P1 i nie ma dzis implementacji.
/// </summary>
public enum PlannerSource
{
    Demo,
    RoadRouting,
    OpenTripPlanner,
    MzkTimetable
}
