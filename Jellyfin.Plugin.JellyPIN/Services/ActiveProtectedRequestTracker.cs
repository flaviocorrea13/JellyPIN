using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.JellyPIN.Services;

public interface IActiveProtectedRequestTracker
{
    IDisposable Track(HttpContext context);
    int AbortAll();
}

public sealed class ActiveProtectedRequestTracker : IActiveProtectedRequestTracker
{
    private readonly ConcurrentDictionary<long, HttpContext> _requests = new();
    private long _nextId;

    public IDisposable Track(HttpContext context)
    {
        var id = Interlocked.Increment(ref _nextId);
        _requests[id] = context;
        return new Registration(_requests, id);
    }

    public int AbortAll()
    {
        var requests = _requests.ToArray();
        foreach (var request in requests)
        {
            request.Value.Abort();
        }

        return requests.Length;
    }

    private sealed class Registration(ConcurrentDictionary<long, HttpContext> requests, long id) : IDisposable
    {
        public void Dispose() => requests.TryRemove(id, out _);
    }
}
