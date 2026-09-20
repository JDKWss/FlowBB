namespace FlowBB.Domain.Routing;

public sealed record RouteStep
{
    public const int InstructionMaxLength = 300;
    public const int LineMaxLength = 40;

    public RouteStep(RouteStepType type, string instruction, int durationMinutes, string? line = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown route step type.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instruction.Length, InstructionMaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(durationMinutes);
        var lineLength = line?.Length ?? 0;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lineLength, LineMaxLength);

        Type = type;
        Instruction = instruction;
        DurationMinutes = durationMinutes;
        Line = line;
    }

    public RouteStepType Type { get; }

    public string Instruction { get; }

    public int DurationMinutes { get; }

    public string? Line { get; }
}
