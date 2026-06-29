namespace ComposableSettings.Document;

/// <summary>
/// Change-equality helpers for document-profile draft setters.
/// Used by generated <c>[SettingsDraftVm]</c> proxy setters and available for manual properties.
/// </summary>
public static class DraftMutation
{
    public static bool TrySet<T>(T current, T value, Action<T> assign, Action onChanged)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return false;

        assign(value);
        onChanged();
        return true;
    }

    public static bool TrySetDouble(double current, double value, Action<double> assign, Action onChanged) =>
        Math.Abs(current - value) < 0.0001
            ? false
            : TrySet(current, value, assign, onChanged);
}
