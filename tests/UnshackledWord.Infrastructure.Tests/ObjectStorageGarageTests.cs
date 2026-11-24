using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.ObjectStorage;

namespace UnshackledWord.Infrastructure.Tests;

/// <summary>
/// Runs against the compose Garage after the Garage setup in docs/deployment.md. Set UNSHACKLEDWORD_TEST_GARAGE=1 plus the
/// OBJECT_STORAGE_* values (UNSHACKLEDWORD_OBJECT_STORAGE_ACCESS_KEY / _SECRET_KEY); service URL defaults to http://localhost:3900.
/// </summary>
public sealed class ObjectStorageGarageTests
{
    [Fact]
    public async Task Put_presigned_get_and_delete_round_trip()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNSHACKLEDWORD_TEST_GARAGE")), "UNSHACKLEDWORD_TEST_GARAGE is not set.");

        var options = new ObjectStorageOptions
        {
            ServiceUrl = Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_SERVICE_URL") ?? "http://localhost:3900",
            PublicServiceUrl = Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_PUBLIC_URL") ?? "http://localhost:3900/media",
            AccessKey = Environment.GetEnvironmentVariable("UNSHACKLEDWORD_OBJECT_STORAGE_ACCESS_KEY") ?? string.Empty,
            SecretKey = Environment.GetEnvironmentVariable("UNSHACKLEDWORD_OBJECT_STORAGE_SECRET_KEY") ?? string.Empty,
        };
        using var storage = new S3ObjectStorage(Options.Create(options));
        var key = $"test/{Guid.NewGuid():N}.txt";
        var body = Encoding.UTF8.GetBytes("hello garage");

        (await storage.BucketReachableAsync()).ShouldBeTrue();
        await storage.PutAsync(key, new MemoryStream(body), "text/plain");

        // Public URL = http://localhost:5173/media goes through the proxy as is. Without a proxy (default public URL has the same
        // host as Garage) the prefix is stripped by hand, which is exactly what the proxy does.
        var url = storage.GetReadUrl(key);
        url.ShouldContain("/media/");
        var proxied = Environment.GetEnvironmentVariable("OBJECT_STORAGE_TEST_PUBLIC_URL") is not null;
        var readUrl = proxied ? url : url.Replace("/media/", "/", StringComparison.Ordinal);
        using var http = new HttpClient();

        using var read = await http.GetAsync(readUrl);
        read.IsSuccessStatusCode.ShouldBeTrue();
        (await read.Content.ReadAsByteArrayAsync()).ShouldBe(body);

        await storage.DeleteAsync(key);
        using var gone = await http.GetAsync(readUrl);
        gone.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
