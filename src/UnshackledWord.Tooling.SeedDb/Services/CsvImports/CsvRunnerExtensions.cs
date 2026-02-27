namespace UnshackledWord.Tooling.SeedDb.Services.CsvImports;

public static partial class CsvRunnerExtensions
{
    static partial void PartialRegisterCsvImportServices(this IServiceCollection services);

    public static IServiceCollection RegisterCsvImportServices(this IServiceCollection services)
    {
        services.PartialRegisterCsvImportServices();
        return services;
    } 
}