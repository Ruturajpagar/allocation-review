using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AllocationReview.Api.Tests.Infrastructure;

public static class Json
{
    /// <summary>The same JSON shape the API produces (camelCase, enums as camelCase strings).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(Options)
        ?? throw new InvalidOperationException($"Response body was not a {typeof(T).Name}.");
}
