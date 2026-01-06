using UnshackledWord.Tooling.BibleTagger.Features.Email;

namespace UnshackledWord.Tooling.BibleTagger.Features.Configuration;

public class AppSettings
{
    public MailKitOptions MailKitOptions { get; set; } = default!;
    public CoreApiSettings CoreApi { get; set; } = default!;
}
