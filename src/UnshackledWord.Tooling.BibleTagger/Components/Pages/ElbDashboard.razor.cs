using Microsoft.AspNetCore.Components;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages;

public partial class ElbDashboard : ComponentBase
{
    public async Task BackupDataAsync()
    {
        await ElbRepo.BackupDataAsync();
    }
}

