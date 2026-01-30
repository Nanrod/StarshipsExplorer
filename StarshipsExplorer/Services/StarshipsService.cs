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

        // Normalize common SWAPI quirks:
        // - "Incom Corporation, Inc." -> ["Incom Corporation, Inc."] (no standalone "Inc.")
        // - "Theed ... Corps/Nubia Star Drives" -> ["Theed ... Corps", "Nubia Star Drives"]
        // - drop "unknown"/"n/a"
        var tokens = manufacturerRaw
            .Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeManufacturerToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        tokens = MergeCorporateSuffixes(tokens);

        return tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeManufacturerToken(string token)
    {
        var t = token.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            return string.Empty;
        }

        // Collapse internal whitespace
        t = string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        // Ignore placeholder values
        if (string.Equals(t, "unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "n/a", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "none", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // Fix a couple of known SWAPI inconsistencies/typos that create duplicate manufacturer options.
        // Keep this list small and obvious (we don't want to "invent" data).
        if (string.Equals(t, "Cyngus Spaceworks", StringComparison.OrdinalIgnoreCase))
        {
            return "Cygnus Spaceworks";
        }

        return t;
    }

    private static List<string> MergeCorporateSuffixes(List<string> tokens)
    {
        if (tokens.Count <= 1)
        {
            return tokens;
        }

        static bool IsCorporateSuffix(string token)
        {
            var t = token.Trim().TrimEnd('.');
            return string.Equals(t, "Inc", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(t, "Incorporated", StringComparison.OrdinalIgnoreCase);
        }

        var merged = new List<string>(tokens.Count);

        foreach (var token in tokens)
        {
            if (IsCorporateSuffix(token))
            {
                // If suffix appears without a preceding company name, ignore it.
                if (merged.Count == 0)
                {
                    continue;
                }

                var suffix = token.Trim().TrimEnd('.');
                suffix = string.Equals(suffix, "Inc", StringComparison.OrdinalIgnoreCase) ? "Inc." : "Incorporated";

                // Avoid duplicating suffix if data is weird like "X, Inc., Inc."
                var prev = merged[^1];
                if (prev.EndsWith(", Inc.", StringComparison.OrdinalIgnoreCase) ||
                    prev.EndsWith(", Incorporated", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                merged[^1] = $"{prev}, {suffix}";
                continue;
            }

            merged.Add(token);
        }

        return merged;
    }
}

