namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Person : SmartEnum<Person>
{
    public static readonly Person First = new(1, ["1"], "First Person", "Erste Person");
    public static readonly Person Second = new(2, ["2"], "Second Person", "Zweite Person");
    public static readonly Person Third = new(3, ["3"], "Third Person", "Dritte Person");

    private Person(int id, string[] abbreviation, string english, string german)
        : base(id, abbreviation, english, german) { }
}
