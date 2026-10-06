# Hacker News Best Stories API

## How to run the application

Requirements:

- .NET 10 SDK

From the repository root, run:

```powershell
dotnet run --project .\HackerNewsBestStories.Api\HackerNewsBestStories.Api.csproj
```

The application URL is printed in the console. For example:

```http
GET https://localhost:7xxx/api/stories?n=10
```

The `n` query parameter is required, must be greater than zero, and cannot exceed `HackerNews:MaxStories` (100 by default).

## Assumptions

- The official `beststories.json` endpoint is used as the source of story IDs, and `item/{id}.json` is used to retrieve story details.
- Results are sorted by the score returned for each story rather than by the order of the IDs returned by Hacker News.
- Deleted or incomplete stories are omitted from the response.
- A story without a `url` uses its Hacker News item URL as the `uri` value.
- The in-memory cache is local to each application instance.

## Enhancements or changes given more time

- Replace the in-memory cache with Redis for multi-instance deployments.
- Add retry and circuit-breaker policies using the built-in `HttpClient` resilience features or Polly.
- Add unit tests for the service and integration tests for the controller.
- Add metrics, distributed tracing, and a health check for the Hacker News dependency.
- Add pagination if the API needs to support more than 100 stories per request.
