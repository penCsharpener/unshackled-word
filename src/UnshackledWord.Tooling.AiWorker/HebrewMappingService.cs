using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewMappingService
{
    private readonly HebrewMappingRepository _repo;
    private readonly GeminiFlashClient _client;
    private readonly ILogger<HebrewMappingService> _logger;

    public HebrewMappingService(HebrewMappingRepository repo, GeminiFlashClient client, ILogger<HebrewMappingService> logger)
    {
        _repo = repo;
        _client = client;
        _logger = logger;
    }
}
