using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dike.Infrastructure.NetSuite;

public static class GlValidationJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new ExactMoneyConverter() }
    };

    public static GlValidationInput Read(ReadOnlySpan<byte> json)
    {
        if (json.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) json = json[3..];
        return JsonSerializer.Deserialize<GlValidationInput>(json, Options) ?? throw new JsonException("Input is required.");
    }

    // Reject nonzero excess precision before decimal parsing can round a long JSON number.
    private sealed class ExactMoneyConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Use a decimal JSON number.");
            var raw = Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);
            if (!NetSuiteMoney.TryParse(raw, out var value))
                throw new JsonException("Money must be an exact decimal(19,2) value without an exponent.");
            return value;
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }
}
