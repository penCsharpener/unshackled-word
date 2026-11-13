using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed;

public record Elb1871Word(BibleReference BibRef, int Order, string InContext, string PlainWord);
