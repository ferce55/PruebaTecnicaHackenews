namespace HackerNewsBestStories.Api.Configuration;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    public int MaxStories { get; set; } = 100;

    public int BestStoriesCacheMinutes { get; set; } = 1;

    public int StoryCacheMinutes { get; set; } = 5;

    public int MaxConcurrentRequests { get; set; } = 8;
}
