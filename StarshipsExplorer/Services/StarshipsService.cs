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
        var all = await GetAllStarshipsAsync(ct);

        if (string.IsNullOrWhiteSpace(manufacturer))
        {
            return all;
        }

        return all
            .Where(s => s.Manufacturers.Any(m => string.Equals(m, manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    //public async Task<IReadOnlyList<string>> GetManufacturersAsync(CancellationToken ct)
    //{
    //    var all = await GetAllStarshipsAsync(ct);
    //    return all
    //        .SelectMany(s => s.Manufacturers)
    //        .Distinct(StringComparer.OrdinalIgnoreCase)
    //        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
    //        .ToArray();
    //}

    private Task<IReadOnlyList<StarshipDto>> GetAllStarshipsAsync(CancellationToken ct) =>
        _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await FetchAllFromSwapiAsync(ct);
        })!;

    private async Task<IReadOnlyList<StarshipDto>> FetchAllFromSwapiAsync(CancellationToken ct)
    {
        const int pageSize = 20;

        var first = await _swapi.GetStarshipsPageAsync(page: 1, limit: pageSize, ct);
        var allListItems = new List<SwapiListItem>(first.TotalRecords);
        allListItems.AddRange(first.Results);

        for (var page = 2; page <= first.TotalPages; page++)
        {
            var next = await _swapi.GetStarshipsPageAsync(page, pageSize, ct);
            allListItems.AddRange(next.Results);
        }

        // SWAPI has multiple calls per item. Keep concurrency modest.
        using var semaphore = new SemaphoreSlim(6);

        var tasks = allListItems.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var response = await _swapi.GetStarshipAsync(item.Uid, ct);
                var props = response.Result.Properties;

                var manufacturerRaw = (props.Manufacturer ?? string.Empty).Trim();
                var manufacturers = ParseManufacturers(manufacturerRaw);

                return new StarshipDto(
                    Uid: response.Result.Uid,
                    Name: (props.Name ?? item.Name ?? string.Empty).Trim(),
                    Model: (props.Model ?? string.Empty).Trim(),
                    StarshipClass: (props.StarshipClass ?? string.Empty).Trim(),
                    Manufacturer: manufacturerRaw,
                    Manufacturers: manufacturers,
                    Crew: (props.Crew ?? string.Empty).Trim(),
                    Passengers: (props.Passengers ?? string.Empty).Trim()
                );
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

