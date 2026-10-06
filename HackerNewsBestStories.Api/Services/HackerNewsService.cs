using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HackerNewsBestStories.Api.Configuration;
using HackerNewsBestStories.Api.Models;

namespace HackerNewsBestStories.Api.Services;

public sealed class HackerNewsService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<HackerNewsOptions> options,
    ILogger<HackerNewsService> logger) : IHackerNewsService
{
    private const string BestStoryIdsCacheKey = "hacker-news-best-story-ids";
    private readonly HackerNewsOptions settings = options.Value;
    private readonly ConcurrentDictionary<long, Lazy<Task<HackerNewsItem?>>> storyLoads = new();

    public async Task<IReadOnlyList<StoryResponse>> GetBestStoriesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var storyIds = await GetBestStoryIdsAsync(cancellationToken);
        var stories = new List<StoryResponse>(count);
        using var throttler = new SemaphoreSlim(settings.MaxConcurrentRequests);

        var storyTasks = storyIds.Select(id => LoadStoryAsync(id, throttler, cancellationToken));
        var loadedStories = await Task.WhenAll(storyTasks);

        foreach (var story in loadedStories
            .Where(story => story is not null)
            .Select(story => story!)
            .OrderByDescending(story => story.Score)
            .Take(count))
        {
            if (string.IsNullOrWhiteSpace(story.Title) || string.IsNullOrWhiteSpace(story.By))
            {
                continue;
            }

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
        return await cache.GetOrCreateAsync(BestStoryIdsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(settings.BestStoriesCacheMinutes);
            var ids = await httpClient.GetFromJsonAsync<long[]>("beststories.json", cancellationToken);
            return ids ?? [];
        }) ?? [];
    }

    private async Task<HackerNewsItem?> LoadStoryAsync(
        long id,
        SemaphoreSlim throttler,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<HackerNewsItem?>(StoryCacheKey(id), out var cachedStory))
        {
            return cachedStory;
        }

        var lazyLoad = storyLoads.GetOrAdd(
            id,
            storyId => new Lazy<Task<HackerNewsItem?>>(
                () => LoadAndCacheStoryAsync(storyId, throttler, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazyLoad.Value;
        }
        finally
        {
            storyLoads.TryRemove(new KeyValuePair<long, Lazy<Task<HackerNewsItem?>>>(id, lazyLoad));
        }
    }

    private async Task<HackerNewsItem?> LoadAndCacheStoryAsync(
        long id,
        SemaphoreSlim throttler,
        CancellationToken cancellationToken)
    {
        await throttler.WaitAsync(cancellationToken);
        try
        {
            var story = await httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", cancellationToken);
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
            throttler.Release();
        }
    }

    private static string StoryCacheKey(long id) => $"hacker-news-story-{id}";
}
