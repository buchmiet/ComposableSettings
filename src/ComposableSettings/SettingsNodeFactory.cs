using Microsoft.Extensions.DependencyInjection;

namespace ComposableSettings;

internal sealed class SettingsNodeFactory(
    IServiceProvider serviceProvider,
    ISettingsComponentNameResolver componentNameResolver,
    SettingsNodeContextAccessor contextAccessor) : ISettingsNodeFactory
{
    public T CreateChild<T>(
        SettingsNodePath parentPath,
        string? instanceName = null)
        where T : class
    {
        return (T)CreateChild(typeof(T), parentPath, instanceName);
    }

    public object CreateChild(
        Type childType,
        SettingsNodePath parentPath,
        string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(childType);
        ArgumentNullException.ThrowIfNull(parentPath);

        var componentName = componentNameResolver.GetComponentName(childType);
        var effectiveInstanceName = SettingsNodePath.ValidateSegment(instanceName ?? componentName);
        var context = new SettingsNodeContext(
            parentPath.Child(effectiveInstanceName),
            effectiveInstanceName,
            componentName,
            childType);

        using var _ = contextAccessor.Push(context);
        return ActivatorUtilities.CreateInstance(serviceProvider, childType);
    }
}
