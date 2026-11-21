using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Fakes;

public sealed class BellekEngelDeposu : IEngelDeposu
{
    private readonly HashSet<(int Engelleyen, int Engellenen)> _engeller = new();
    private readonly Lock _kilit = new();

    public Task EngelleAsync(int engelleyenId, int engellenenId, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            _engeller.Add((engelleyenId, engellenenId));
        }
        return Task.CompletedTask;
    }

    public Task EngeliKaldirAsync(int engelleyenId, int engellenenId)
    {
        lock (_kilit)
        {
            _engeller.Remove((engelleyenId, engellenenId));
        }
        return Task.CompletedTask;
    }

    public Task<bool> EngelliMiAsync(int engelleyenId, int engellenenId)
    {
        lock (_kilit)
        {
            return Task.FromResult(_engeller.Contains((engelleyenId, engellenenId)));
        }
    }

    public Task<IReadOnlySet<int>> EngellediklerimAsync(int engelleyenId)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlySet<int>>(
                _engeller.Where(e => e.Engelleyen == engelleyenId).Select(e => e.Engellenen).ToHashSet());
        }
    }

    public Task<IReadOnlySet<int>> BeniEngelleyenlerAsync(int engellenenId)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlySet<int>>(
                _engeller.Where(e => e.Engellenen == engellenenId).Select(e => e.Engelleyen).ToHashSet());
        }
    }
}
