namespace UnshackledWord.Domain.Models.Settings;

/// <summary>S3-compatible object storage (Garage, roadmap 0022). Config section <c>ObjectStorage</c>; without a <see cref="ServiceUrl"/> the feature is off.</summary>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Internal S3 endpoint the API talks to, e.g. <c>http://unshackledword.garage:3900</c>.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL browsers read from, including the proxy prefix, e.g. <c>http://localhost:5173/media</c> or <c>https://example.org/media</c>.
    /// The signature covers its host (and port); the proxy strips the path prefix and forwards the Host header unchanged.
    /// </summary>
    public string PublicServiceUrl { get; set; } = string.Empty;

    /// <summary>Must equal <c>s3_region</c> in garage.toml; a mismatch is answered with a bare 400.</summary>
    public string Region { get; set; } = "garage";

    public string Bucket { get; set; } = "unshackledword-media";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl);
}
