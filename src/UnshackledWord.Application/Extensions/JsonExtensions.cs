using System.Text.Json;

namespace UnshackledWord.Application.Extensions;

public static class JsonExtensions
{
    private static JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public static string ToIndentedJson(this object obj)
    {
        return JsonSerializer.Serialize(obj, obj.GetType(), _jsonSerializerOptions);
    }

    public static string ToIndentedJson<T>(this T obj)
    {
        return JsonSerializer.Serialize<T>(obj, _jsonSerializerOptions);
    }

    public static string ToNonIndentedJson<T>(this T obj)
    {
        return JsonSerializer.Serialize<T>(obj);
    }
}
