using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Infrastructure.ObjectStorage;

/// <summary>
/// Garage (or any S3 API) over AWSSDK.S3. Region is explicit because Garage answers a mismatch with a bare 400.
/// Read URLs are signed for the public origin (host and port are part of the SigV4 signature) and then get the proxy's
/// path prefix (<c>/media</c>) in front. The proxy strips that prefix again and forwards the Host header byte for byte.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    private readonly ObjectStorageOptions _options;
    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signer;
    private readonly string _publicPrefix;
    private readonly Protocol _publicProtocol;

    public S3ObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        _client = new AmazonS3Client(credentials, Config(_options.ServiceUrl));

        var publicUri = new Uri(_options.PublicServiceUrl);
        _publicProtocol = publicUri.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS;
        _publicPrefix = publicUri.AbsolutePath.TrimEnd('/');
        _signer = new AmazonS3Client(credentials, Config(publicUri.GetLeftPart(UriPartial.Authority)));

        AmazonS3Config Config(string url) => new()
        {
            ServiceURL = url,
            AuthenticationRegion = _options.Region,
            ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _client.PutObjectAsync(
            new PutObjectRequest { BucketName = _options.Bucket, Key = key, InputStream = content, ContentType = contentType, UseChunkEncoding = false },
            cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);

    public string GetReadUrl(string key, TimeSpan? lifetime = null)
    {
        var signed = _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Protocol = _publicProtocol,
            Expires = DateTime.UtcNow.Add(lifetime ?? DefaultLifetime),
        });
        var uri = new Uri(signed);

        return $"{uri.GetLeftPart(UriPartial.Authority)}{_publicPrefix}{uri.PathAndQuery}";
    }

    public async Task<bool> BucketReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetBucketLocationAsync(_options.Bucket, cancellationToken);

            return true;
        }
        catch (AmazonServiceException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _signer.Dispose();
    }
}
