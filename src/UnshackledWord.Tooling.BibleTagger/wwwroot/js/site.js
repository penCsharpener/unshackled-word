window.initializeKeyboardShortcut = function () {
  document.addEventListener("keydown", function (event) {
    if (event.ctrlKey && event.key === "Enter") {
      document.getElementById('saveButton').click();
    }
  });
};

window.setDotNetReference = function (dotNetReference) {
  window.dotNetReference = dotNetReference;
};

window.startNotificationTimer = (notificationId) => {
  setTimeout(() => {
    // Find the notification element and trigger its close method
    console.log(notificationId);
    let notification = document.getElementById(notificationId);
    if (notification) {
      console.log('notification', notification);
      // Use JS Interop to call the component method to close
      //notification.__blazorComponent.invokeMethodAsync("CloseFromJs");
      // DotNet.invokeMethodAsync("UnshackledWord.Tooling.BibleTagger", "CloseFromJs");
      window.dotNetReference.invokeMethodAsync("CloseFromJs");
    }
  }, 3000); // 3 seconds timer
}

window.bibleInterop = {
  initHighlighting: function (containerId) {
    const container = document.getElementById(containerId);
    if (!container) {
      return;
    }

    container.addEventListener('mouseover', (e) => {
      const strongs = e.target.getAttribute('data-strongs');
      if (strongs) {
        // Select all elements with this specific Strong's number
        const matches = container.querySelectorAll(`[data-strongs="${strongs}"]`);
        matches.forEach(el => el.classList.add('highlight-strongs'));
      }
    });

    container.addEventListener('mouseout', (e) => {
      const strongs = e.target.getAttribute('data-strongs');
      if (strongs) {
        const matches = container.querySelectorAll(`[data-strongs="${strongs}"]`);
        matches.forEach(el => el.classList.remove('highlight-strongs'));
      }
    });
  }
};

window.getBoundingClientRect = (element) => {
  return element.getBoundingClientRect();
};

window.listenForOutsideClick = (dotNetHelper, containerId) => {
  const listener = (event) => {
    const container = document.getElementById(containerId);
    // If the click is outside the popup container, notify C# to close it
    if (container && !container.contains(event.target)) {
      dotNetHelper.invokeMethodAsync('InvokeClose');
      document.removeEventListener('click', listener); // Cleanup
    }
  };
  // Timeout prevents the click that OPENED the popup from immediately closing it
  setTimeout(() => document.addEventListener('click', listener), 10);
};
