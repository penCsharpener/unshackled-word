using System.Reflection;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>The app version from Directory.Build.props (no commit suffix).</summary>
public static class AppVersion
{
    public static readonly string Current = (typeof(AppVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0];
}
