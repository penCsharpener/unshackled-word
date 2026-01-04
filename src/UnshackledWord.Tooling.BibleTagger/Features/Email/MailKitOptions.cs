namespace UnshackledWord.Tooling.BibleTagger.Features.Email;

public class MailKitOptions
{
    /// <summary>
    /// SMTP Server address
    /// </summary>
    public string Server { get; set; } = default!;

    /// <summary>
    /// SMTP Server Port ,default is 25
    /// </summary>
    public int Port { get; set; } = 25;

    /// <summary>
    /// send user name
    /// </summary>
    public string SenderName { get; set; } = default!;

    /// <summary>
    /// send user email
    /// </summary>
    public string SenderEmail { get; set; } = default!;

    /// <summary>
    /// send user account,may be equal to senderemail
    /// </summary>
    public string Account { get; set; } = default!;

    /// <summary>
    /// send user password
    /// </summary>
    public string Password { get; set; } = default!;

    /// <summary>
    /// enable security
    /// </summary>
    public bool Security { get; set; } = false;
}
