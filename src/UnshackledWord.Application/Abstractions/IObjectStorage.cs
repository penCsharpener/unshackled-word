namespace UnshackledWord.Application.Abstractions;

/// <summary>Object storage for user-visible files (vocab audio). Registered only when <c>ObjectStorage</c> is configured.</summary>
public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Presigned GET URL on the public (proxied) base URL; computed locally, no request.</summary>
    string GetReadUrl(string key, TimeSpan? lifetime = null);

    /// <summary>True when the bucket answers a HEAD request.</summary>
    Task<bool> BucketReachableAsync(CancellationToken cancellationToken = default);
}
