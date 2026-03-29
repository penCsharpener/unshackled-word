namespace UnshackledWord.Infrastructure.Repositories;

public static class PropertyListHelper
{
    public static string[] GetPropertyNames(object obj)
    {
        var type = obj.GetType();

        var properties = type.GetProperties();

        return properties.Select(p => p.Name).ToArray();
    }
}
