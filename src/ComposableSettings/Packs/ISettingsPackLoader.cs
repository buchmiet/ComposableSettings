namespace ComposableSettings.Packs;

public interface ISettingsPackLoader
{
    ValueTask<SettingsPackLoadResult?> LoadAsync(string packPath, CancellationToken cancellationToken = default);
}
