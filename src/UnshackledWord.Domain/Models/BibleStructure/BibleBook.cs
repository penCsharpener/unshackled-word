namespace UnshackledWord.Domain.Models.BibleStructure;

public record struct BibleBook
{
    public int Id { get; }
    public string Name { get; }
    public string[] Abbreviations { get; }

    public BibleBook(int id, string name, string[] abbreviations)
    {
        Id = id;
        Name = name;
        Abbreviations = abbreviations;
    }

    public static Dictionary<int, BibleBook> NewTestamentBooks => new()
    {
        { 1, new BibleBook(1, "Genesis", ["Gen", "Ge", "Gn"]) },
        { 2, new BibleBook(2, "Exodus", ["Exod", "Ex", "Exo"]) },
        { 3, new BibleBook(3, "Leviticus", ["Lev", "Le", "Lv"]) },
        { 4, new BibleBook(4, "Numbers", ["Num", "Nu", "Nm"]) },
        { 5, new BibleBook(5, "Deuteronomy", ["Deut", "Dt", "Deu"]) },
        { 6, new BibleBook(6, "Joshua", ["Josh", "Jos", "Jsh"]) },
        { 7, new BibleBook(7, "Judges", ["Judg", "Jdg", "Jgs"]) },
        { 8, new BibleBook(8, "Ruth", ["Ruth", "Ru", "Rt"]) },
        { 9, new BibleBook(9, "1 Samuel", ["1 Sam", "1Sa", "1st Sam"]) },
        { 10, new BibleBook(10, "2 Samuel", ["2 Sam", "2Sa", "2nd Sam"]) },
        { 11, new BibleBook(11, "1 Kings", ["1 Kings", "1Ki", "1st Kings"]) },
        { 12, new BibleBook(12, "2 Kings", ["2 Kings", "2Ki", "2nd Kings"]) },
        { 13, new BibleBook(13, "1 Chronicles", ["1 Chron", "1Ch", "1st Chron"]) },
        { 14, new BibleBook(14, "2 Chronicles", ["2 Chron", "2Ch", "2nd Chron"]) },
        { 15, new BibleBook(15, "Ezra", ["Ezra", "Ezr"]) },
        { 16, new BibleBook(16, "Nehemiah", ["Neh", "Ne"]) },
        { 17, new BibleBook(17, "Esther", ["Esth", "Es", "Est"]) },
        { 18, new BibleBook(18, "Job", ["Job", "Jb"]) },
        { 19, new BibleBook(19, "Psalms", ["Ps", "Psa", "Psalms"]) },
        { 20, new BibleBook(20, "Proverbs", ["Prov", "Pr", "Prv"]) },
        { 21, new BibleBook(21, "Ecclesiastes", ["Eccl", "Ecc", "Qoheleth"]) },
        { 22, new BibleBook(22, "Song of Solomon", ["Song", "SoS", "Canticles", "Song of Songs"]) },
        { 23, new BibleBook(23, "Isaiah", ["Isa", "Is"]) },
        { 24, new BibleBook(24, "Jeremiah", ["Jer", "Je", "Jr"]) },
        { 25, new BibleBook(25, "Lamentations", ["Lam", "La"]) },
        { 26, new BibleBook(26, "Ezekiel", ["Ezek", "Eze", "Ez"]) },
        { 27, new BibleBook(27, "Daniel", ["Dan", "Da", "Dn"]) },
        { 28, new BibleBook(28, "Hosea", ["Hos", "Ho"]) },
        { 29, new BibleBook(29, "Joel", ["Joel", "Jl"]) },
        { 30, new BibleBook(30, "Amos", ["Amos", "Am"]) },
        { 31, new BibleBook(31, "Obadiah", ["Obad", "Ob"]) },
        { 32, new BibleBook(32, "Jonah", ["Jon", "Jnh"]) },
        { 33, new BibleBook(33, "Micah", ["Mic", "Mi"]) },
        { 34, new BibleBook(34, "Nahum", ["Nah", "Na"]) },
        { 35, new BibleBook(35, "Habakkuk", ["Hab", "Hb"]) },
        { 36, new BibleBook(36, "Zephaniah", ["Zeph", "Zep", "Zp"]) },
        { 37, new BibleBook(37, "Haggai", ["Hag", "Hg"]) },
        { 38, new BibleBook(38, "Zechariah", ["Zech", "Zec", "Zc"]) },
        { 39, new BibleBook(39, "Malachi", ["Mal", "Ml"]) },
        { 40, new BibleBook(40, "Matthew", ["Matt", "Mt", "Mat"]) },
        { 41, new BibleBook(41, "Mark", ["Mk", "Mrk", "Mar"]) },
        { 42, new BibleBook(42, "Luke", ["Lk", "Lu"]) },
        { 43, new BibleBook(43, "John", ["Jn", "Jhn", "Joh"]) },
        { 44, new BibleBook(44, "Acts", ["Acts", "Ac", "Apg"]) },
        { 45, new BibleBook(45, "Romans", ["Rom", "Ro", "Rm", "Rö"]) },
        { 46, new BibleBook(46, "1 Corinthians", ["1 Cor", "1Co", "1 Kor", "1Ko"]) },
        { 47, new BibleBook(47, "2 Corinthians", ["2 Cor", "2Co", "2 Kor", "2Ko"]) },
        { 48, new BibleBook(48, "Galatians", ["Gal", "Ga"]) },
        { 49, new BibleBook(49, "Ephesians", ["Eph", "Ep"]) },
        { 50, new BibleBook(50, "Philippians", ["Phil", "Php", "Pp"]) },
        { 51, new BibleBook(51, "Colossians", ["Col", "Co", "Kol"]) },
        { 52, new BibleBook(52, "1 Thessalonians", ["1 Thess", "1Th"]) },
        { 53, new BibleBook(53, "2 Thessalonians", ["2 Thess", "2Th"]) },
        { 54, new BibleBook(54, "1 Timothy", ["1 Tim", "1Ti"]) },
        { 55, new BibleBook(55, "2 Timothy", ["2 Tim", "2Ti"]) },
        { 56, new BibleBook(56, "Titus", ["Tit", "Ti"]) },
        { 57, new BibleBook(57, "Philemon", ["Philem", "Phm"]) },
        { 58, new BibleBook(58, "Hebrews", ["Heb", "He"]) },
        { 59, new BibleBook(59, "James", ["Jas", "Jm", "Jak"]) },
        { 60, new BibleBook(60, "1 Peter", ["1 Pet", "1Pe", "1Pt"]) },
        { 61, new BibleBook(61, "2 Peter", ["2 Pet", "2Pe", "2Pt"]) },
        { 62, new BibleBook(62, "1 John", ["1 Jn", "1Jo", "1Joh"]) },
        { 63, new BibleBook(63, "2 John", ["2 Jn", "2Jo", "2Joh"]) },
        { 64, new BibleBook(64, "3 John", ["3 Jn", "3Jo", "3Joh"]) },
        { 65, new BibleBook(65, "Jude", ["Jude", "Jud"]) },
        { 66, new BibleBook(66, "Revelation", ["Rev", "Re", "Offb"]) }
    };
}
