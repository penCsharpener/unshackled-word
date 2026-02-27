using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.CsvImports;

public partial class CsvRunner : IRunner
{
    public partial Task Run(CancellationToken token = default);
}

/*
// add CsvRunner.local.cs next to this class
public partial class CsvRunner
   {
       public partial async Task Run(CancellationToken token = default)
       {
       }
   }
 */
