using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using HackerNewsBestStories.Api.Configuration;
using HackerNewsBestStories.Api.Services;

namespace HackerNewsBestStories.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StoriesController(
    IHackerNewsService hackerNewsService,
    IOptions<HackerNewsOptions> options) : ControllerBase
{
    private readonly HackerNewsOptions settings = options.Value;

    [HttpGet]
    public async Task<IActionResult> GetBestStories(
        [FromQuery] int? n,
        CancellationToken cancellationToken)
    {
        if (n is null or <= 0)
        {
            return BadRequest(new { error = "Query parameter 'n' must be greater than zero." });
        }

        if (n > settings.MaxStories)
        {
            return BadRequest(new
            {
                error = $"Query parameter 'n' cannot be greater than {settings.MaxStories}."
            });
        }

        try
        {
            var stories = await hackerNewsService.GetBestStoriesAsync(n.Value, cancellationToken);
            return Ok(stories);
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hacker News is temporarily unavailable."
            });
        }
    }
}
