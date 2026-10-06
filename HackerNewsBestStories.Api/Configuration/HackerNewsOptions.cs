using System.ComponentModel.DataAnnotations;

namespace HackerNewsBestStories.Api.Configuration;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    [Range(1, 1000)]
    public int MaxStories { get; set; } = 100;

    [Range(1, 1440)]
    public int BestStoriesCacheMinutes { get; set; } = 1;

    [Range(1, 1440)]
    public int StoryCacheMinutes { get; set; } = 5;

    [Range(1, 64)]
    public int MaxConcurrentRequests { get; set; } = 8;
}
