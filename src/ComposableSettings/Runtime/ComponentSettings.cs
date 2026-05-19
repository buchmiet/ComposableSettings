using ComposableSettings.Abstractions;

namespace ComposableSettings.Runtime;

public  class ComponentSettings<TSettings>(
    ISettingsNodeContext context,
    IComponentSettingsStore store) : IComponentSettings<TSettings>
    where TSettings : class, new()
{
    public SettingsNodePath Path => context.Path;

    public TSettings Value => store.Get<TSettings>(context.Path);

    public void Save(TSettings value)
    {
        store.Set(context.Path, value);
    }

    public Task SaveAsync(TSettings value, CancellationToken cancellationToken = default)
    {
        store.Set(context.Path, value);
        return Task.CompletedTask;
    }
}