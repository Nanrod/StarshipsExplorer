using Microsoft.Extensions.Caching.Memory;

namespace StarshipsExplorer.App.Starships;

public sealed class StarshipsService
{
    private const string CacheKey = "swapi.starships.all.v1";
    private readonly SwapiClient _swapi;
    private readonly IMemoryCache _cache;

    public StarshipsService(SwapiClient swapi, IMemoryCache cache)
    {
        _swapi = swapi;
        _cache = cache;
    }

    public async Task<IReadOnlyList<StarshipDto>> GetStarshipsAsync(string? manufacturer, CancellationToken ct)
    {
        var all = await GetAllStarshipsAsync(progress: null, ct);

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return all;
        }

        return all
            .Where(s => s.Manufacturers.Any(m => string.Equals(m, manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public async Task<IReadOnlyList<StarshipDto>> GetStarshipsAsync(
        string? manufacturer,
        IProgress<StarshipsLoadProgress>? progress,
        CancellationToken ct)
    {
        var all = await GetAllStarshipsAsync(progress, ct);

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return all;
        }

        return all
            .Where(s => s.Manufacturers.Any(m => string.Equals(m, manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private async Task<IReadOnlyList<StarshipDto>> GetAllStarshipsAsync(
        IProgress<StarshipsLoadProgress>? progress,
        CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<StarshipDto>? cached) && cached is not null)
        {
            progress?.Report(new StarshipsLoadProgress(Loaded: cached.Count, Total: cached.Count));
            return cached;
        }

        var created = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await FetchAllFromSwapiAsync(progress, ct);
        });

        if (created is null)
        {
            throw new InvalidOperationException("Failed to create starships cache entry.");
        }

        progress?.Report(new StarshipsLoadProgress(Loaded: created.Count, Total: created.Count));
        return created;
    }

    private async Task<IReadOnlyList<StarshipDto>> FetchAllFromSwapiAsync(
        IProgress<StarshipsLoadProgress>? progress,
        CancellationToken ct)
    {
        const int pageSize = 20;

        var first = await _swapi.GetStarshipsPageAsync(page: 1, limit: pageSize, ct);
        progress?.Report(new StarshipsLoadProgress(Loaded: 0, Total: first.TotalRecords));

        var allListItems = new List<SwapiListItem>(first.TotalRecords);
        allListItems.AddRange(first.Results);

        for (var page = 2; page <= first.TotalPages; page++)
        {
            var next = await _swapi.GetStarshipsPageAsync(page, pageSize, ct);
            allListItems.AddRange(next.Results);
        }

        // SWAPI has multiple calls per item. Keep concurrency modest.
        using var semaphore = new SemaphoreSlim(6);
        var loaded = 0;
        var total = allListItems.Count;

        var tasks = allListItems.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var response = await _swapi.GetStarshipAsync(item.Uid, ct);
                var props = response.Result.Properties;

                var manufacturerRaw = (props.Manufacturer ?? string.Empty).Trim();
                var manufacturers = ParseManufacturers(manufacturerRaw);

                var dto = new StarshipDto(
                    Uid: response.Result.Uid,
                    Name: (props.Name ?? item.Name ?? string.Empty).Trim(),
                    Model: (props.Model ?? string.Empty).Trim(),
                    StarshipClass: (props.StarshipClass ?? string.Empty).Trim(),
                    Manufacturer: manufacturerRaw,
                    Manufacturers: manufacturers,
                    Crew: (props.Crew ?? string.Empty).Trim(),
                    Passengers: (props.Passengers ?? string.Empty).Trim()
                );

                var nowLoaded = System.Threading.Interlocked.Increment(ref loaded);
                progress?.Report(new StarshipsLoadProgress(Loaded: nowLoaded, Total: total, CurrentItemName: dto.Name));

                return dto;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ParseManufacturers(string manufacturerRaw)
    {
        if (string.IsNullOrWhiteSpace(manufacturerRaw))
        {
            return Array.Empty<string>();
        }

        return manufacturerRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

