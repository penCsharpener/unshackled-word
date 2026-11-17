using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>The <c>Role</c> column: the default <c>head</c> is written as an empty cell, an empty or missing cell reads as <c>head</c>.</summary>
public sealed class RoleConverter : DefaultTypeConverter
{
    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        return value is null or "" or "head" ? string.Empty : value.ToString();
    }

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return string.IsNullOrWhiteSpace(text) ? "head" : text.Trim();
    }
}

/// <summary>A flag column that is written only when true (empty cell = false) and reads <c>true</c>, <c>1</c> and <c>True</c> as true.</summary>
public sealed class TrueOnlyFlagConverter : DefaultTypeConverter
{
    public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        return value is true ? "true" : string.Empty;
    }

    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return text is not null && (text.Trim() == "1" || text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
    }
}
