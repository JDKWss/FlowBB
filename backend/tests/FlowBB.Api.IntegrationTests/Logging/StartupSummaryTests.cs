using FlowBB.Api.Logging;
using FluentAssertions;

namespace FlowBB.Api.IntegrationTests.Logging;

public sealed class StartupSummaryTests
{
    [Theory]
    [InlineData(null, "(not set)")]
    [InlineData("", "(not set)")]
    [InlineData("   ", "(not set)")]
    [InlineData("neo4j+s://abc.databases.neo4j.io", "abc.databases.neo4j.io")]
    [InlineData("neo4j+s://neo4juser:s3cretpass@abc.databases.neo4j.io:7687", "abc.databases.neo4j.io")]
    [InlineData("bolt://localhost:7687", "localhost")]
    [InlineData("not a uri", "(invalid)")]
    public void DescribeNeo4jHost_ReturnsOnlyTheHostName(string? uri, string expected)
    {
        var host = StartupSummary.DescribeNeo4jHost(uri);

        host.Should().Be(expected);
        host.Should().NotContain("s3cretpass").And.NotContain("neo4juser").And.NotContain("7687");
    }
}
