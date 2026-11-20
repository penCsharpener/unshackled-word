using System.Diagnostics;
using OpenTelemetry;

namespace UnshackledWord.Infrastructure.Observability;

/// <summary>Removes query strings from server and client spans: search terms and provider keys must never reach traces (roadmap 0021).</summary>
public sealed class SpanSanitizer : BaseProcessor<Activity>
{
    private static readonly string[] FullUrlTags = ["url.full", "http.url"];

    public override void OnEnd(Activity data)
    {
        data.SetTag("url.query", null);
        data.SetTag("http.query", null);
        foreach (var tag in FullUrlTags)
        {
            if (data.GetTagItem(tag) is string url)
            {
                data.SetTag(tag, StripQuery(url));
            }
        }

        if (data.GetTagItem("http.target") is string target)
        {
            data.SetTag("http.target", StripQuery(target));
        }
    }

    public static string StripQuery(string url)
    {
        var cut = url.IndexOfAny(['?', '#']);

        return cut < 0 ? url : url[..cut];
    }
}
