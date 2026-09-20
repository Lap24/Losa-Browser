# 🌐 Losa Browser

**Losa Browser** is a lightweight, privacy‑focused WebView2 browser built for speed, simplicity, and customization.  
Designed for Windows, it’s perfect for developers and power users who want a minimal browser that still feels powerful.

---

## 🚀 Features
- ⚡ Fast startup and smooth navigation using Microsoft WebView2
- 🧩 Modular sidebar with custom buttons and quick actions
- 🔒 Privacy‑first design — no telemetry, no tracking
- 🎨 Easy to theme and extend with your own scripts
- 🧠 Built with .NET 6.0 (Windows Desktop)

---

## 🛠️ Installation

1. **Install .NET 6.0 Desktop Runtime**  
   If you don’t have it, the installer will open the official download page automatically:  
   [Download .NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

2. **Run the setup script**  
   Double‑click `start.bat` to:
   - Build the project  
   - Dont delete the folder from downloads  
   - Create a desktop launcher (`LosaBrowser.bat`)

3. **Launch the browser**  
   Use the desktop shortcut or run:
   ```bat
   start "" "%USERPROFILE%\Downloads\Losa Browser\bin\Release\net6.0-windows\LosaBrowser.exe"
