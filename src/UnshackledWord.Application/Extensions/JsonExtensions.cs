using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace UnshackledWord.Application.Extensions;

public static class JsonExtensions
{
    private static JavaScriptEncoder _unshackledJsEncoder = JavaScriptEncoder.Create(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Latin1Supplement,
        UnicodeRanges.GreekandCoptic,
        UnicodeRanges.GreekExtended,
        UnicodeRanges.Hebrew,
        UnicodeRanges.CombiningDiacriticalMarks);
    private static JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = _unshackledJsEncoder
    };

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
        return JsonSerializer.Serialize<T>(obj, new  JsonSerializerOptions { Encoder = _unshackledJsEncoder });
    }
}
