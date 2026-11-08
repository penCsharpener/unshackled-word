using FastEndpoints;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.WebApi.MetaBible.GetBibleStructure;

namespace UnshackledWord.Tooling.WebApi.Endpoints.MetaBible.GetBibleStructure;

public class Endpoint : EndpointWithoutRequest<GetBibleStructureResponse>
{
    private static readonly GetBibleStructureResponse Structure = Build();

    public override void Configure()
    {
        Get("structure");
        Group<RouteGroupConfig>();
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(Structure, ct);

    private static GetBibleStructureResponse Build()
    {
        var books = BibleBook.AllBooks.Values
            .OrderBy(b => b.Id)
            .Select(b => new BibleStructureBook
            {
                Id = b.Id,
                Testament = b.Id <= 39 ? "ot" : "nt",
                NameDe = GermanBookNames.GetName(b.Id),
                NameEn = b.Name,
                AbbreviationDe = GermanBookNames.GetAbbreviation(b.Id),
                AbbreviationEn = b.Abbreviations[0],
                VersesPerChapter = b.VersesPerChapterHeb
            })
            .ToList();

        return new GetBibleStructureResponse { Books = books };
    }
}
