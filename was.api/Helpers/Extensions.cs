using System.Text.Json;

namespace was.api.Helpers
{
    public static class Extensions
    {
        public static string ToJsonString(this object obj)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true, // Optional: for pretty print
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Optional: camelCase
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull // Optional: ignore nulls
            };

            return JsonSerializer.Serialize(obj, options);
        }
    }
}
