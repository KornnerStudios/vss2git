using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceSafe.Json
{
    public sealed class VssItemNameJsonConverter : JsonConverter<Logical.VssItemName>
    {
        public override Logical.VssItemName Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? jsonString = reader.GetString();

            if (string.IsNullOrEmpty(jsonString))
            {
                throw new JsonException($"Null or empty value provided for non-nullable {nameof(Logical.VssItemName)}");
            }

            return Logical.VssItemName.Parse(jsonString)!;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Logical.VssItemName itemName,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(itemName.ToString());
    };

    public sealed class VssItemNameNullableJsonConverter : JsonConverter<Logical.VssItemName?>
    {
        public override Logical.VssItemName? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
                Logical.VssItemName.Parse(reader.GetString()!);

        public override void Write(
            Utf8JsonWriter writer,
            Logical.VssItemName? itemName,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(itemName?.ToString());
    };
}
