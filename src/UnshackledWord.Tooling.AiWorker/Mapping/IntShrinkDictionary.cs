using System.Collections.Concurrent;

namespace UnshackledWord.Tooling.AiWorker.Mapping;

public sealed class IntShrinkDictionary : ConcurrentDictionary<int, int>
{
    private ConcurrentDictionary<int, int> _reverseDictionary = new();

    public int Increment { get; set; } = 1;

    public void Reset()
    {
        Increment = 1;
        Clear();
        _reverseDictionary.Clear();
    }

    public int AddIds(int originalId)
    {
        TryAdd(originalId, Increment);
        _reverseDictionary.TryAdd(Increment, originalId);
        var returnId = Increment;

        Increment++;

        return returnId;
    }

    public int GetOriginalId(int reducedId)
    {
        return _reverseDictionary[reducedId];
    }
}
