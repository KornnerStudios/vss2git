using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceSafe.Json
{
    public sealed class VssItemNameJsonConverter : JsonConverter<VssItemName>
    {
        public override VssItemName Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string? jsonString = reader.GetString();

            if (string.IsNullOrEmpty(jsonString))
            {
                throw new JsonException($"Null or empty value provided for non-nullable {nameof(VssItemName)}");
            }

            return VssItemName.Parse(jsonString)!;
        }

        public override void Write(
            Utf8JsonWriter writer,
            VssItemName itemName,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(itemName.ToString());
    };

    public sealed class VssItemNameNullableJsonConverter : JsonConverter<VssItemName?>
    {
        public override VssItemName? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
                VssItemName.Parse(reader.GetString()!);

        public override void Write(
            Utf8JsonWriter writer,
            VssItemName? itemName,
            JsonSerializerOptions options) =>
                writer.WriteStringValue(itemName?.ToString());
    };
}
