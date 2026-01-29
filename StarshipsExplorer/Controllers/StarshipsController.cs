using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarshipsExplorer.App.Starships;

namespace StarshipsExplorer.App.Controllers;

[ApiController]
[Authorize]
[Route("api/starships")]
public sealed class StarshipsController : ControllerBase
{
    private readonly StarshipsService _starships;

    public StarshipsController(StarshipsService starships)
    {
        _starships = starships;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StarshipDto>>> GetStarships([FromQuery] string? manufacturer, CancellationToken ct)
    {
        var results = await _starships.GetStarshipsAsync(manufacturer, ct);
        return Ok(results);
    }

    //[HttpGet("manufacturers")]
    //public async Task<ActionResult<IReadOnlyList<string>>> GetManufacturers(CancellationToken ct)
    //{
    //    var results = await _starships.GetManufacturersAsync(ct);
    //    return Ok(results);
    //}
}

