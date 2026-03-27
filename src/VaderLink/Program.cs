using VaderLink;
using VaderLink.Config;

// Single-instance guard: prevent the user from accidentally launching VaderLink twice
// (two instances would fight over vJoy and the vendor HID interface).
using var mutex = new Mutex(initiallyOwned: true, "VaderLink-SingleInstance", out bool createdNew);
if (!createdNew)
{
    MessageBox.Show(
        "VaderLink is already running.\n\nCheck the system tray.",
        "VaderLink",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    return;
}

ApplicationConfiguration.Initialize();

// High-DPI support
Application.SetHighDpiMode(HighDpiMode.SystemAware);

var config = ConfigManager.Load();

using var app = new App(config);
app.Run();

// Run the WinForms message pump on the main thread.
// The tray icon and all UI interactions happen here.
Application.Run();
