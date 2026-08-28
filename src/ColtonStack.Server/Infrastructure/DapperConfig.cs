using System.Data;
using System.Globalization;
using Dapper;

namespace ColtonStack.Server.Infrastructure;

/// <summary>
/// SQLite has no native DateTimeOffset column type; these handlers make the round-trip explicit:
/// stored as ISO-8601 "O" text, parsed back with round-trip semantics. Registered once at startup
/// (see <see cref="Register"/>) — no reflection mapping, no hidden conventions.
/// </summary>
public static class DapperConfig
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // Replace Dapper's default mappings so both reads and writes flow through the handlers.
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new BoolHandler());
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);

        public override DateTimeOffset Parse(object value) =>
            value switch
            {
                string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => throw new InvalidCastException($"Cannot map value of type {value.GetType()} to DateTimeOffset."),
            };
    }

    /// <summary>SQLite booleans are integers (0/1); make that conversion explicit instead of accidental.</summary>
    private sealed class BoolHandler : SqlMapper.TypeHandler<bool>
    {
        public override void SetValue(IDbDataParameter parameter, bool value) => parameter.Value = value ? 1L : 0L;

        public override bool Parse(object value) =>
            value switch
            {
                long number => number != 0,
                int number => number != 0,
                _ => throw new InvalidCastException($"Cannot map value of type {value.GetType()} to bool."),
            };
    }
}
