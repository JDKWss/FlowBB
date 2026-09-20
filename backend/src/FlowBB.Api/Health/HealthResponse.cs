namespace FlowBB.Api.Health;

/// <summary>Odpowiedz <c>/health</c> i <c>/health/ready</c>; odpowiada schematowi HealthResponse z contracts/openapi.yaml.</summary>
public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);
