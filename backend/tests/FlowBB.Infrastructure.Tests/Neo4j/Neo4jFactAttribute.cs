namespace FlowBB.Infrastructure.Tests.Neo4j;

/// <summary>
/// Test wymagajacy prawdziwej instancji Neo4j. Bez zmiennych <c>FLOWBB_NEO4J_TEST_*</c> jest pomijany (Skipped),
/// a nie zaliczany: zielony przebieg bez bazy nie dowodzi, ze adapter dziala.
/// </summary>
public sealed class Neo4jFactAttribute : FactAttribute
{
    public Neo4jFactAttribute()
    {
        if (!Neo4jTestEnvironment.IsConfigured)
        {
            Skip = $"Brak {Neo4jTestEnvironment.UriVariable} i {Neo4jTestEnvironment.PasswordVariable}: test wymaga prawdziwej instancji Neo4j.";
        }
    }
}
