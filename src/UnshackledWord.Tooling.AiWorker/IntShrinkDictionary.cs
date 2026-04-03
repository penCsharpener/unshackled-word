namespace UnshackledWord.Tooling.AiWorker;

public sealed class IntShrinkDictionary : Dictionary<int, int>
{
    private Dictionary<int, int> _reverseDictionary = new();

    public int Increment { get; set; } = 1;

    public void Reset()
    {
        Increment = 1;
        Clear();
        _reverseDictionary.Clear();
    }

    public int AddIds(int originalId)
    {
        Add(originalId, Increment);
        _reverseDictionary.Add(Increment, originalId);
        var returnId = Increment;

        Increment++;

        return returnId;
    }

    public int GetOriginalId(int reducedId)
    {
        return _reverseDictionary[reducedId];
    }
}
