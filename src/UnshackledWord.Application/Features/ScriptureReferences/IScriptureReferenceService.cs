using UnshackledWord.Domain.WebApi.MetaBible.ParseReference;

namespace UnshackledWord.Application.Features.ScriptureReferences;

public interface IScriptureReferenceService
{
    /// <summary>Parses text typed in German citation style (search box, MCP). Never throws; plain text yields no references and no error.</summary>
    ParseReferenceResponse Parse(string? query);
}
