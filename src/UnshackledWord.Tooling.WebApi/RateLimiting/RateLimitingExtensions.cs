using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace UnshackledWord.Tooling.WebApi.RateLimiting;

public static class RateLimitingExtensions
{
    private static readonly JsonSerializerOptions BodyJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Binds and validates both option sections; registers the limiter only when <c>RateLimiting:Enabled</c> is true.</summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>().Bind(configuration.GetSection(RateLimitingOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();

        var enabled = configuration.GetSection(RateLimitingOptions.SectionName).GetValue<bool>(nameof(RateLimitingOptions.Enabled));
        if (!enabled)
        {
            return services;
        }

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitingOptions>, ILoggerFactory>(Configure);
        return services;
    }

    private static void Configure(RateLimiterOptions limiter, IOptions<RateLimitingOptions> settings, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("UnshackledWord.Tooling.WebApi.RateLimiting");
        var unknownLogged = 0;

        var general = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (RateLimitPartitioner.IsExemptPath(context.Request.Path.Value))
            {
                return RateLimitPartition.GetNoLimiter("health");
            }

            var key = RateLimitPartitioner.GeneralKey(IsAuthenticated(context), RemoteIp(context));
            return Partition(key, settings.Value.General, context, logger, ref unknownLogged);
        });

        var strict = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (RateLimitPartitioner.IsExemptPath(context.Request.Path.Value))
            {
                return RateLimitPartition.GetNoLimiter("health");
            }

            var key = RateLimitPartitioner.StrictKey(IsAuthenticated(context), GroupOf(context), RemoteIp(context));
            return Partition(key, settings.Value.Strict, context, logger, ref unknownLogged);
        });

        // A strict-tier request counts toward both limiters: strict is a sub-budget of general.
        limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(general, strict);
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = async (rejected, ct) =>
        {
            var context = rejected.HttpContext;
            var group = GroupOf(context);
            var tier = group is not null && strict.GetStatistics(context)?.CurrentAvailablePermits < 1
                ? group
                : RateLimitPartitioner.GeneralTier;
            var options = tier == RateLimitPartitioner.GeneralTier ? settings.Value.General : settings.Value.Strict;

            var retryAfter = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var span)
                ? (int)Math.Ceiling(span.TotalSeconds)
                : Math.Max(1, options.WindowSeconds / Math.Max(1, options.SegmentsPerWindow));
            retryAfter = Math.Max(1, retryAfter);

            logger.LogWarning("Rate limit hit: tier {Tier}, client {Client}, path {Path}", tier, RateLimitPartitioner.ShortenForLog(RemoteIp(context)), context.Request.Path.Value);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = retryAfter.ToString();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { code = "rateLimited", tier, retryAfterSeconds = retryAfter }, BodyJson), ct);
        };
    }

    private static RateLimitPartition<string> Partition(string? key, RateLimitTierOptions tier, HttpContext context, ILogger logger, ref int unknownLogged)
    {
        if (key is null)
        {
            return RateLimitPartition.GetNoLimiter("none");
        }

        if (RemoteIp(context) is null && Interlocked.Exchange(ref unknownLogged, 1) == 0)
        {
            logger.LogWarning("Request without a remote IP address: such requests share one rate-limit bucket");
        }

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = tier.PermitLimit,
            Window = TimeSpan.FromSeconds(tier.WindowSeconds),
            SegmentsPerWindow = tier.SegmentsPerWindow,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private static bool IsAuthenticated(HttpContext context) => context.User.Identity?.IsAuthenticated == true;

    private static IPAddress? RemoteIp(HttpContext context) => context.Connection.RemoteIpAddress;

    private static string? GroupOf(HttpContext context) => context.GetEndpoint()?.Metadata.GetMetadata<RateLimitGroupMetadata>()?.Group;
}
