window.initializeKeyboardShortcut = function () {
  document.addEventListener("keydown", function (event) {
    if (event.ctrlKey && event.key === "Enter") {
      document.getElementById('saveButton').click();
    }
  });
};
