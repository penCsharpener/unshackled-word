window.initializeKeyboardShortcut = function () {
  document.addEventListener("keydown", function (event) {
    if (event.ctrlKey && event.key === "Enter") {
      document.getElementById('saveButton').click();
    }
  });
};

window.startNotificationTimer = (notificationId) => {
  setTimeout(() => {
    // Find the notification element and trigger its close method
    let notification = document.getElementById(notificationId);
    if (notification) {
      // Use JS Interop to call the component method to close
      notification.__blazorComponent.invokeMethodAsync("CloseFromJs");
    }
  }, 3000); // 3 seconds timer
}
