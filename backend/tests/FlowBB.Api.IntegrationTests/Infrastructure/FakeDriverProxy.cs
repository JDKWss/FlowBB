using System.Reflection;
using Neo4j.Driver;

namespace FlowBB.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Fake <see cref="IDriver"/> oparty na <see cref="DispatchProxy"/> (bez nowych pakietow). Obsluguje tylko
/// <c>VerifyConnectivityAsync</c> i zwalnianie zasobow; kazde inne wywolanie jest bledem testu.
/// </summary>
public class FakeDriverProxy : DispatchProxy
{
    private Func<Task> _verifyConnectivity = () => Task.CompletedTask;

    public static IDriver Create(Func<Task> verifyConnectivity)
    {
        var proxy = Create<IDriver, FakeDriverProxy>();
        ((FakeDriverProxy)(object)proxy)._verifyConnectivity = verifyConnectivity;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
    {
        nameof(IDriver.VerifyConnectivityAsync) => _verifyConnectivity(),
        nameof(IDisposable.Dispose) => null,
        nameof(IAsyncDisposable.DisposeAsync) => default(ValueTask),
        _ => throw new NotSupportedException($"FakeDriverProxy does not support {targetMethod?.Name}.")
    };
}
