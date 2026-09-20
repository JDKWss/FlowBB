namespace FlowBB.Infrastructure.Tests.Neo4j;

internal sealed class Neo4jIntegrationFactAttribute : FactAttribute
{
    public const string OptInVariable = "NEO4J_RUN_INTEGRATION_TESTS";

    public Neo4jIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(OptInVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {OptInVariable}=true to run tests against a real Neo4j instance.";
        }
    }
}
