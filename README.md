# Starships Explorer (Blazor + BFF)

Simple full-stack app using **Blazor Server** + an authenticated **BFF JSON API** (ASP.NET Core **Controllers**) to fetch Starships from `swapi.tech` (no SWAPI client libraries).

## Run

From the repo root:

```bash
dotnet run --project .\StarshipsExplorer.App
```

Optional (run on a known URL/port):

```bash
dotnet run --project .\StarshipsExplorer.App --urls http://localhost:5080
```

Then browse to the URL printed in the console (or `http://localhost:5080` if you used the command above).

## Login (static credentials)

Configured in `StarshipsExplorer.App/appsettings.json`:

- **Username**: `luke`
- **Password**: `usetheforce`

## UI requirements

After logging in, go to:

- `GET /starships`

The page:

- renders a `<select>` of **manufacturer** values
- renders a `<table>` of Starships
- filters the table when a manufacturer is selected
- shows all Starships when no manufacturer is selected
- shows a **spinner + progress bar** while Starships are loading (e.g. `0 / 36` → `36 / 36`)

## BFF JSON API (Controllers, requires authentication)

- **GET** ` /api/starships`
  - Optional query param: `manufacturer`
  - Example: `GET /api/starships?manufacturer=Kuat%20Drive%20Yards`

## Auth endpoints (Controllers)

- **POST** `/auth/login` (form post; sets auth cookie; redirects)
- **GET** `/auth/logout` (clears auth cookie; redirects)

Notes:

- **Performance**: the first load may take several seconds because SWAPI’s list endpoint does not include `manufacturer`, so the app fetches starship details per item. Results are cached in-memory for ~10 minutes, so subsequent loads are fast.
- **Manufacturer normalization**: SWAPI data can be inconsistent. The app normalizes manufacturer tokens by:
  - splitting on commas and `/`
  - merging corporate suffixes (prevents standalone `Inc` / `Inc.` / `Incorporated` entries)
  - dropping placeholder values like `unknown` / `n/a` / `none`
  - fixing a small typo (`Cyngus Spaceworks` → `Cygnus Spaceworks`)

