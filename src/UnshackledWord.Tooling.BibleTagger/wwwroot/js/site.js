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
