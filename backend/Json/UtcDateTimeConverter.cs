using System.Text.Json;
using System.Text.Json.Serialization;

namespace DailyChallenges.Json
{
    /// <summary>
    /// Writes every DateTime as ISO-8601 UTC with a trailing "Z".
    /// All timestamps are stored as UTC, but SQLite hands them back with Kind=Unspecified, so
    /// those are relabelled with SpecifyKind (no clock shift). Calling ToUniversalTime on an
    /// Unspecified value would treat it as server-local time and shift it.
    /// System.Text.Json applies this converter to DateTime? as well.
    /// </summary>
    public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            // An explicit offset ("+01:00") is parsed into Local; normalize it back to UTC.
            return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(ToUtc(value));
        }

        internal static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            // A Local value really is local time, so converting is correct here.
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
