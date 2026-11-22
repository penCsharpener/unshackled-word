using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.ObjectStorage;
using UnshackledWord.Infrastructure.Vocab;

namespace UnshackledWord.Infrastructure.Tests;

public sealed class VocabAudioTests
{
    [Fact]
    public void Mp3_with_id3_header_is_accepted()
    {
        VocabAudio.LooksLikeMp3("ID3\u0004\0\0\0\0\0\0"u8).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0xFB)]
    [InlineData(0xF3)]
    [InlineData(0xE3)]
    public void Mp3_frame_sync_is_accepted(int second)
    {
        VocabAudio.LooksLikeMp3([0xFF, (byte)second, 0x90, 0x00]).ShouldBeTrue();
    }

    [Fact]
    public void Wav_riff_header_is_rejected()
    {
        VocabAudio.LooksLikeMp3("RIFF\0\0\0\0WAVEfmt "u8).ShouldBeFalse();
    }

    [Fact]
    public void Ogg_m4a_and_text_are_rejected()
    {
        VocabAudio.LooksLikeMp3("OggS\0\u0002\0\0"u8).ShouldBeFalse();
        VocabAudio.LooksLikeMp3("\0\0\0 ftypM4A "u8).ShouldBeFalse();
        VocabAudio.LooksLikeMp3("hello"u8).ShouldBeFalse();
    }

    [Fact]
    public void Empty_and_one_byte_are_rejected()
    {
        VocabAudio.LooksLikeMp3([]).ShouldBeFalse();
        VocabAudio.LooksLikeMp3([0xFF]).ShouldBeFalse();
        VocabAudio.LooksLikeMp3([(byte)'I']).ShouldBeFalse();
    }

    [Fact]
    public void Key_follows_the_scheme_and_changes_with_the_content()
    {
        var a = VocabAudio.KeyFor("G", 56, "ID3a"u8);
        var b = VocabAudio.KeyFor("G", 56, "ID3b"u8);

        a.ShouldMatch(@"^vocab/g/56-[0-9a-f]{8}\.mp3$");
        b.ShouldNotBe(a);
        VocabAudio.KeyFor("G", 56, "ID3a"u8).ShouldBe(a);
        VocabAudio.KeyFor("H", 7, "ID3a"u8).ShouldStartWith("vocab/h/7-");
    }

    [Fact]
    public void Url_is_null_without_storage_or_key()
    {
        VocabAudio.UrlFor(null, "vocab/g/1-abc.mp3").ShouldBeNull();
        VocabAudio.UrlFor(null, null).ShouldBeNull();
    }

    [Fact]
    public async Task Garage_replace_leaves_only_the_new_object()
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
        var first = VocabAudio.KeyFor("G", 999999, "ID3one"u8);
        var second = VocabAudio.KeyFor("G", 999999, "ID3two"u8);
        await storage.PutAsync(first, new MemoryStream(Encoding.ASCII.GetBytes("ID3one")), VocabAudio.ContentType);
        await storage.PutAsync(second, new MemoryStream(Encoding.ASCII.GetBytes("ID3two")), VocabAudio.ContentType);
        await storage.DeleteAsync(first);
        using var http = new HttpClient();

        using var gone = await http.GetAsync(storage.GetReadUrl(first).Replace("/media/", "/", StringComparison.Ordinal));
        using var kept = await http.GetAsync(storage.GetReadUrl(second).Replace("/media/", "/", StringComparison.Ordinal));
        gone.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        kept.IsSuccessStatusCode.ShouldBeTrue();
        await storage.DeleteAsync(second);
    }
}
