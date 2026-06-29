namespace ComposableSettings.Packs;

public interface ISettingsPackLoader
{
    Task<SettingsPackLoadResult?> LoadAsync(string packPath, CancellationToken cancellationToken = default);
}
