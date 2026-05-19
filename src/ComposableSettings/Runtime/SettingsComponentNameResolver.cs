using System.Reflection;

namespace ComposableSettings;

public sealed class SettingsComponentNameResolver : ISettingsComponentNameResolver
{
    private const string ViewModelSuffix = "ViewModel";

    public string GetComponentName(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);

        var attributeName = componentType.GetCustomAttribute<SettingsComponentAttribute>()?.Name;
        var componentName = attributeName ?? GetDefaultComponentName(componentType);
        return SettingsNodePath.ValidateSegment(componentName);
    }

    private static string GetDefaultComponentName(Type componentType)
    {
        var name = componentType.Name;
        return name.EndsWith(ViewModelSuffix, StringComparison.Ordinal)
            ? name[..^ViewModelSuffix.Length]
            : name;
    }
}
