using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public record Elb1871Word(BibleReference BibRef, int Order, string InContext, string PlainWord);
