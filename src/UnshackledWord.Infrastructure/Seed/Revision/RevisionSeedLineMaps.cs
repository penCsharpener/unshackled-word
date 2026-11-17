using CsvHelper.Configuration;
using UnshackledWord.Application.Features.Seed.Revision;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary><c>revision/levels.csv</c>: <c>Level,Code,NameDe,NameEn,DescriptionDe,DescriptionEn,IsDefault</c>.</summary>
public sealed class RevisionLevelSeedLineMap : ClassMap<RevisionLevelSeedLine>
{
    public RevisionLevelSeedLineMap()
    {
        Map(x => x.Level).Index(0).Name("Level");
        Map(x => x.Code).Index(1).Name("Code");
        Map(x => x.NameDe).Index(2).Name("NameDe");
        Map(x => x.NameEn).Index(3).Name("NameEn");
        Map(x => x.DescriptionDe).Index(4).Name("DescriptionDe");
        Map(x => x.DescriptionEn).Index(5).Name("DescriptionEn");
        Map(x => x.IsDefault).Index(6).Name("IsDefault");
    }
}

/// <summary><c>revision/verses/NN-Book.csv</c>: <c>Ref,Level,BasedOnLevel,BaseHash,Text,Note,Reviewed</c>.</summary>
public sealed class RevisionVerseSeedLineMap : ClassMap<RevisionVerseSeedLine>
{
    public RevisionVerseSeedLineMap()
    {
        Map(x => x.Ref).Index(0).Name("Ref");
        Map(x => x.Level).Index(1).Name("Level");
        Map(x => x.BasedOnLevel).Index(2).Name("BasedOnLevel");
        Map(x => x.BaseHash).Index(3).Name("BaseHash");
        Map(x => x.Text).Index(4).Name("Text");
        Map(x => x.Note).Index(5).Name("Note");
        Map(x => x.Reviewed).Index(6).Name("Reviewed");
    }
}

/// <summary><c>revision/mapping/NN-Book.csv</c>: <c>Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin,Role,Strongs,PartLemma,MultiHead</c>; the last four (roadmap 0018) are empty for the defaults and missing in older files, which are detected by their header lacking <c>Role</c>.</summary>
public sealed class RevisionMappingSeedLineMap : ClassMap<RevisionMappingSeedLine>
{
    public RevisionMappingSeedLineMap()
    {
        Map(x => x.Ref).Index(0).Name("Ref");
        Map(x => x.Level).Index(1).Name("Level");
        Map(x => x.Pos).Index(2).Name("Pos");
        Map(x => x.Form).Index(3).Name("Form");
        Map(x => x.Part).Index(4).Name("Part");
        Map(x => x.SrcRef).Index(5).Name("SrcRef");
        Map(x => x.SrcPos).Index(6).Name("SrcPos");
        Map(x => x.SrcHash).Index(7).Name("SrcHash");
        Map(x => x.Added).Index(8).Name("Added");
        Map(x => x.ParentPos).Index(9).Name("ParentPos");
        Map(x => x.ParentForm).Index(10).Name("ParentForm");
        Map(x => x.Origin).Index(11).Name("Origin");
        Map(x => x.Role).Index(12).Name("Role").TypeConverter<RoleConverter>().Optional();
        Map(x => x.Strongs).Index(13).Name("Strongs").Optional();
        Map(x => x.PartLemma).Index(14).Name("PartLemma").Optional();
        Map(x => x.MultiHead).Index(15).Name("MultiHead").TypeConverter<TrueOnlyFlagConverter>().Optional();
    }
}
