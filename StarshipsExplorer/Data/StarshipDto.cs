namespace StarshipsExplorer.App.Starships;

public sealed record StarshipDto(
    string Uid,
    string Name,
    string Model,
    string StarshipClass,
    string Manufacturer,
    string[] Manufacturers,
    string Crew,
    string Passengers
);

