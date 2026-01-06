namespace UnshackledWord.Application.Repositories;

public record CreateMappingResult(int InsertedMappingsCount, List<int> UpdatedElbWordIds);
