using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HackerNewsBestStories.Api.Configuration;
using HackerNewsBestStories.Api.Models;

namespace HackerNewsBestStories.Api.Services;

public sealed class HackerNewsService : IHackerNewsService
{
    private const string BestStoryIdsCacheKey = "hacker-news-best-story-ids";
    private readonly HttpClient httpClient;
    private readonly IMemoryCache cache;
    private readonly HackerNewsOptions settings;
    private readonly ILogger<HackerNewsService> logger;
    private readonly SemaphoreSlim requestLimiter;
    private readonly ConcurrentDictionary<long, Lazy<Task<HackerNewsItem?>>> storyLoads = new();

    public HackerNewsService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<HackerNewsOptions> options,
        ILogger<HackerNewsService> logger)
    {
        this.httpClient = httpClient;
        this.cache = cache;
        settings = options.Value;
        this.logger = logger;
        requestLimiter = new SemaphoreSlim(settings.MaxConcurrentRequests);
    }

    public async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var storyIds = await GetBestStoryIdsAsync(cancellationToken);
        var stories = new List<StoryResponse>(count);

        var storyTasks = storyIds.Select(id => LoadStoryAsync(id, cancellationToken));
        var loadedStories = await Task.WhenAll(storyTasks);

        foreach (var story in loadedStories
            .Where(story => story is not null)
            .Select(story => story!)
            .Where(IsValidStory)
            .OrderByDescending(story => story.Score)
            .Take(count))
        {
            stories.Add(new StoryResponse(
                story.Title,
                story.Url ?? $"https://news.ycombinator.com/item?id={story.Id}",
                story.By,
                DateTimeOffset.FromUnixTimeSeconds(story.Time),
                story.Score,
                story.CommentCount));
        }

        return stories;
    }

    private async Task<long[]> GetBestStoryIdsAsync(CancellationToken cancellationToken)
    {
        var loadTask = cache.GetOrCreateAsync(BestStoryIdsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(settings.BestStoriesCacheMinutes);
            var ids = await httpClient.GetFromJsonAsync<long[]>("beststories.json");
            return ids ?? [];
        });

        return await loadTask.WaitAsync(cancellationToken) ?? [];
    }

    private async Task<HackerNewsItem?> LoadStoryAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<HackerNewsItem?>(StoryCacheKey(id), out var cachedStory))
        {
            return cachedStory;
        }

        var lazyLoad = storyLoads.GetOrAdd(
            id,
            storyId => new Lazy<Task<HackerNewsItem?>>(
                () => LoadAndCacheStoryAsync(storyId),
                LazyThreadSafetyMode.ExecutionAndPublication));

        var loadTask = lazyLoad.Value;
        _ = loadTask.ContinueWith(
            _ => storyLoads.TryRemove(new KeyValuePair<long, Lazy<Task<HackerNewsItem?>>>(id, lazyLoad)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return await loadTask.WaitAsync(cancellationToken);
    }

    private async Task<HackerNewsItem?> LoadAndCacheStoryAsync(
        long id)
    {
        await requestLimiter.WaitAsync();
        try
        {
            var story = await httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json");
            if (story is not null)
            {
                cache.Set(StoryCacheKey(id), story, TimeSpan.FromMinutes(settings.StoryCacheMinutes));
            }

            return story;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Unable to retrieve Hacker News story {StoryId}", id);
            return null;
        }
        finally
        {
            requestLimiter.Release();
        }
    }

    private static bool IsValidStory(HackerNewsItem story) =>
        !string.IsNullOrWhiteSpace(story.Title) &&
        !string.IsNullOrWhiteSpace(story.By) &&
        story.Time > 0;

    private static string StoryCacheKey(long id) => $"hacker-news-story-{id}";
}
