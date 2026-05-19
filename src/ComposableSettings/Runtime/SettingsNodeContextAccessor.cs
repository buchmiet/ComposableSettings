using ComposableSettings.Abstractions;

namespace ComposableSettings.Runtime;

internal  class SettingsNodeContextAccessor
{
    private readonly AsyncLocal<ISettingsNodeContext?> _current = new();

    public ISettingsNodeContext Current =>
        _current.Value ?? throw new InvalidOperationException(
            "No settings node context is active. Create components with ISettingsNodeFactory so runtime settings path can be supplied.");

    public IDisposable Push(ISettingsNodeContext context)
    {
        var previous = _current.Value;
        _current.Value = context;
        return new RestoreContext(this, previous);
    }

    private  class RestoreContext(
        SettingsNodeContextAccessor accessor,
        ISettingsNodeContext? previous) : IDisposable
    {
        public void Dispose()
        {
            accessor._current.Value = previous;
        }
    }
}