using System.Text.Json.Serialization;

namespace StarshipsExplorer.App.Starships;

public sealed record SwapiListResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("total_records")] int TotalRecords,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    [property: JsonPropertyName("results")] List<SwapiListItem> Results
);

public sealed record SwapiListItem(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url
);

public sealed record SwapiStarshipResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("result")] SwapiStarshipResult Result
);

public sealed record SwapiStarshipResult(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("properties")] SwapiStarshipProperties Properties
);

public sealed record SwapiStarshipProperties(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("manufacturer")] string? Manufacturer,
    [property: JsonPropertyName("starship_class")] string? StarshipClass,
    [property: JsonPropertyName("crew")] string? Crew,
    [property: JsonPropertyName("passengers")] string? Passengers
);

