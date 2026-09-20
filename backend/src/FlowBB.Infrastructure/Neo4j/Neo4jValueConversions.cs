using Neo4j.Driver;

namespace FlowBB.Infrastructure.Neo4j;

/// <summary>Konwersje miedzy typami domeny a wartosciami Neo4j. Identyfikatory Guid sa w grafie tekstem w formacie "D".</summary>
internal static class Neo4jValueConversions
{
    public static string ToDatabaseId(Guid id)
    {
        return id.ToString("D");
    }

    public static Guid FromDatabaseId(string id)
    {
        return Guid.ParseExact(id, "D");
    }

    public static DateTimeOffset ToDateTimeOffset(object? value, string fieldName)
    {
        return value switch
        {
            ZonedDateTime zoned => zoned.ToDateTimeOffset(),
            LocalDateTime local => new DateTimeOffset(local.ToDateTime(), TimeSpan.Zero),
            _ => throw new InvalidCastException($"Field {fieldName} is not a datetime value.")
        };
    }

    public static DateTimeOffset? ToNullableDateTimeOffset(object? value, string fieldName)
    {
        return value is null ? null : ToDateTimeOffset(value, fieldName);
    }

    /// <summary>Parsuje nazwe wartosci enuma. Liczby i nieznane nazwy sa bledem (<see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/> je przyjmuje).</summary>
    public static TEnum ToEnum<TEnum>(object? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (value is string text && Enum.GetNames<TEnum>().Contains(text, StringComparer.Ordinal))
        {
            return Enum.Parse<TEnum>(text);
        }

        throw new InvalidCastException($"Field {fieldName} does not contain a known {typeof(TEnum).Name} name.");
    }
}
