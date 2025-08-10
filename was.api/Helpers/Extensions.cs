using System.Globalization;
using System.Linq.Expressions;
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
        public static string ToISTString(this DateTime utcDateTime)
        {
            // Ensure the input DateTime is in UTC
            if (utcDateTime.Kind != DateTimeKind.Utc)
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            // Find IST TimeZone
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

            // Convert to IST
            DateTime istDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, istZone);

            // Format as "DD-MM-YYYY hh:mm:a"
            return istDateTime.ToString("dd-MM-yyyy hh:mm:tt", CultureInfo.InvariantCulture).ToLower();
        }
        public static IQueryable<T> WhereIf<T>(
            this IQueryable<T> query,
            bool condition,
            Expression<Func<T, bool>> predicate)
            {
                return condition ? query.Where(predicate) : query;
            }

    }
   
}
