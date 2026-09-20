using FluentAssertions;

namespace FlowBB.Infrastructure.Tests.Neo4j;

public sealed class Neo4jTestSafetyGuardTests
{
    [Fact]
    public void EnsureDisposable_AuraUri_IsRejectedWithoutLeakingCredentials()
    {
        const string uri = "neo4j+s://test-user:top-secret@sample.databases.neo4j.io";

        var action = () => Neo4jTestSafetyGuard.EnsureDisposable(uri, "true");

        var exception = action.Should().Throw<InvalidOperationException>()
            .Which;
        exception.Message.Should().Contain("Aura");
        exception.Message.Should().NotContain("test-user");
        exception.Message.Should().NotContain("top-secret");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    public void EnsureDisposable_WithoutExplicitConfirmation_IsRejected(string? confirmation)
    {
        var action = () => Neo4jTestSafetyGuard.EnsureDisposable("neo4j://127.0.0.1:17687", confirmation);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{Neo4jTestEnvironment.ConfirmDisposableVariable}=true*");
    }

    [Fact]
    public void EnsureDisposable_ConfirmedLocalDatabase_IsAccepted()
    {
        var action = () => Neo4jTestSafetyGuard.EnsureDisposable("neo4j://127.0.0.1:17687", " TRUE ");

        action.Should().NotThrow();
    }
}
