namespace StarshipsExplorer.App.Starships;

public sealed record StarshipsLoadProgress(
    int Loaded,
    int Total,
    string? CurrentItemName = null
);

