namespace FlaUI.Mcp.Core;

public class ScreenshotCache
{
    private readonly Dictionary<string, byte[]> _baselines = new();
    private readonly object _sync = new();

    public void Store(string scopeKey, byte[] png)
    {
        lock (_sync) { _baselines[scopeKey] = png; }
    }

    public bool TryTake(string scopeKey, out byte[] png)
    {
        lock (_sync)
        {
            if (_baselines.TryGetValue(scopeKey, out var found))
            {
                _baselines.Remove(scopeKey);
                png = found;
                return true;
            }
            png = Array.Empty<byte>();
            return false;
        }
    }

    public bool Has(string scopeKey)
    {
        lock (_sync) { return _baselines.ContainsKey(scopeKey); }
    }

    public int Count
    {
        get { lock (_sync) { return _baselines.Count; } }
    }
}
