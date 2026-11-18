using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.ScriptureReferences;

/// <summary>Turns lexicon segments into the API contract, adding the reader path of each reference.</summary>
public static class LexiconSegmentMapper
{
    public static List<LexiconSegmentResponse>? ToResponse(IReadOnlyList<LexiconSegment> segments)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        return segments.Select(segment =>
        {
            var response = new LexiconSegmentResponse { Text = segment.Text };
            if (segment.Ref is { } reference)
            {
                var path = ReaderPath.Create(new ScriptureReference(reference.BookId, null, reference.Spans, 0, 0, false));
                if (path is not null)
                {
                    response.Ref = new LexiconReferenceResponse
                    {
                        BookId = reference.BookId,
                        Path = path,
                        Spans = reference.Spans
                            .Select(s => new LexiconSpanResponse { StartChapter = s.StartChapter, StartVerse = s.StartVerse, EndChapter = s.EndChapter, EndVerse = s.EndVerse })
                            .ToList(),
                    };
                }
            }

            return response;
        }).ToList();
    }
}
