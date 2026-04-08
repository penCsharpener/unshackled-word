namespace UnshackledWord.Application.Features.Backup;

public class ElbMappingBackup
{
    public int HebRefId {get;set;}
    public int PositionInVerse {get;set;}
    public string? GermanWordPart {get;set;}
    public string ElbWord { get; set; } = default!;
    public string? StepWord {get;set;}
    public int? StepPositionInVerse {get;set;}
    public bool IsAddedWord {get;set;}
    public int? ParentPositionInVerse {get;set;}
    public string? ParentWord {get;set;}
}
