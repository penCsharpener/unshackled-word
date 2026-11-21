using FluentValidation;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Application.Abstractions;

public sealed class ObjectStorageOptionsValidator : AbstractValidator<ObjectStorageOptions>
{
    public ObjectStorageOptionsValidator()
    {
        RuleFor(x => x.ServiceUrl).Must(IsHttpUrl).WithMessage("ObjectStorage:ServiceUrl must be an absolute http(s) URL.");
        RuleFor(x => x.PublicServiceUrl).Must(IsHttpUrl).WithMessage("ObjectStorage:PublicServiceUrl must be an absolute http(s) URL.");
        RuleFor(x => x.Region).NotEmpty();
        RuleFor(x => x.Bucket).NotEmpty();
        RuleFor(x => x.AccessKey).NotEmpty();
        RuleFor(x => x.SecretKey).NotEmpty();
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
