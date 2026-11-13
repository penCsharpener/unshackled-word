using System.Globalization;
using System.Security.Cryptography;
using CsvHelper;
using CsvHelper.Configuration;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>
/// The one place that decides how seed CSVs look: CsvHelper, invariant culture, header line, LF line endings (also after the last
/// line), UTF-8 without BOM. Same input always gives the same bytes, so an unchanged database exports an unchanged file.
/// </summary>
public static class SeedCsv
{
    private static CsvConfiguration Configuration() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        NewLine = "\n"
    };

    /// <summary>Reading accepts LF and CRLF (a checkout with <c>core.autocrlf</c> turns the seed files into CRLF).</summary>
    private static CsvConfiguration ReadConfiguration() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true
    };

    /// <summary>The CSV bytes of the records (header plus one line each) and their SHA-256.</summary>
    public static (byte[] Bytes, string Sha256) Serialize<T, TMap>(IEnumerable<T> records)
        where TMap : ClassMap<T>
    {
        using var stream = new MemoryStream();

        using (var writer = new StreamWriter(stream, SeedLayout.Utf8NoBom, leaveOpen: true))
        using (var csv = new CsvWriter(writer, Configuration()))
        {
            csv.Context.RegisterClassMap<TMap>();
            csv.WriteHeader<T>();
            csv.NextRecord();

            foreach (var record in records)
            {
                csv.WriteRecord(record);
                csv.NextRecord();
            }
        }

        var bytes = stream.ToArray();

        return (bytes, Sha256Hex(bytes));
    }

    public static List<T> Read<T, TMap>(byte[] bytes)
        where TMap : ClassMap<T>
    {
        using var reader = new StreamReader(new MemoryStream(bytes), SeedLayout.Utf8NoBom);
        using var csv = new CsvReader(reader, ReadConfiguration());
        csv.Context.RegisterClassMap<TMap>();

        return csv.GetRecords<T>().ToList();
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
