using System.Reflection;

namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public abstract class SmartEnum<T> where T : SmartEnum<T>
{
    public int Id { get; }
    public string[] Abbreviation { get; }
    public string EnglishName { get; }
    public string GermanName { get; }

    protected SmartEnum(int id, string[] abbreviation, string english, string german)
    {
        Id = id;
        Abbreviation = abbreviation;
        EnglishName = english;
        GermanName = german;
    }

    private static readonly Dictionary<int, T> _all = typeof(T)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(f => typeof(T).IsAssignableFrom(f.FieldType))
        .Select(f => (T)f.GetValue(null)!)
        .ToDictionary(x => x.Id);

    public static IEnumerable<T> List()
    {
        return _all.Values;
    }

    public static T? FromId(int id)
    {
        return _all.GetValueOrDefault(id);
    }

    public static T? FromAbbreviation(string? abbr)
    {
        return _all.Values.FirstOrDefault(x => x.Abbreviation.Contains(abbr, StringComparer.OrdinalIgnoreCase));
    }
}
