using Microsoft.AspNetCore.Components;

namespace UnshackledWord.Tooling.BibleTagger.Components.Pages.ReadElberfelder;

public class WordPopupService
{
    public event Func<ElementReference, string, Task>? OnWordSelected;

    public async Task ShowPopup(ElementReference element, string strongs)
    {
        if (OnWordSelected is not null)
        {
            await OnWordSelected.Invoke(element, strongs);
        }
    }
}
