using FlowBB.Infrastructure.Neo4j;
using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

public sealed class Neo4jOptionsTests
{
    [Fact]
    public void DefaultDatabase_IsTheOnlyDatabaseOfNeo4jCommunity()
    {
        // Community nie pozwala utworzyc drugiej bazy, wiec domyslna "flowbb" konczyla sie bledem polaczenia.
        Neo4jOptions.DefaultDatabase.Should().Be("neo4j");
    }
}
