using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Repositories;

public static class PropertyListHelper
{
    public static string[] GetPropertyNames(object obj)
    {
        var type = obj.GetType();

        var properties = type.GetProperties();
        var names = properties.Select(p => p.Name).ToArray();

        return names;
    }

    public static (string QuotedNames, string ParameterNames) GetAllNames(object obj)
    {
        var type = obj.GetType();

        var properties = type.GetProperties();
        var names = properties.Select(p => p.Name).ToArray();
        var quotedNames = names.Select(x => $"\"{x}\"").JoinStrings(",");
        var parameterNames = names.Select(x => $"@{x}").JoinStrings(",");
        return (quotedNames, parameterNames);
    }
}
