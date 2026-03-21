using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public record Elb1871Verse(BibleReference BibRef, string Text, List<Elb1871Word> Words);
