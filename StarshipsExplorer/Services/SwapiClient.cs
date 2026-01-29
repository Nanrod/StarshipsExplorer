using System.Net.Http.Json;

namespace StarshipsExplorer.App.Starships;

public sealed class SwapiClient
{
    private readonly HttpClient _http;

    public SwapiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<SwapiListResponse> GetStarshipsPageAsync(int page, int limit, CancellationToken ct)
    {
        var response = await _http.GetFromJsonAsync<SwapiListResponse>(
            $"/api/starships?page={page}&limit={limit}",
            ct);

        if (response is null)
        {
            throw new InvalidOperationException("Unexpected null response from SWAPI (starships list).");
        }

        return response;
    }

    public async Task<SwapiStarshipResponse> GetStarshipAsync(string uid, CancellationToken ct)
    {
        var response = await _http.GetFromJsonAsync<SwapiStarshipResponse>($"/api/starships/{uid}", ct);

        if (response is null)
        {
            throw new InvalidOperationException($"Unexpected null response from SWAPI for starship '{uid}'.");
        }

        return response;
    }
}

