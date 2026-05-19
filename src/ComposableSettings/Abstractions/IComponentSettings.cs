using ComposableSettings.Runtime;

namespace ComposableSettings.Abstractions;

public interface IComponentSettings<TSettings>
    where TSettings : class, new()
{
    SettingsNodePath Path { get; }

    TSettings Value { get; }

    void Save(TSettings value);

    Task SaveAsync(TSettings value, CancellationToken cancellationToken = default);
}