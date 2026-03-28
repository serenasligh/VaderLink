# ***V A D E R L I N K***
A niche solution for the lads n ladies out there who love Keysticks and all the extra buttons on the Vader 5 Pro gamepad. 💖🎮

# Installation

## Step 1 — Install .NET 8 SDK

Download and install the **.NET 8 SDK (x64)** from Microsoft:
`https://dotnet.microsoft.com/en-us/download/dotnet/8.0`

Pick the **SDK** (not just the Runtime) since you need it to build. After installing, open a new Command Prompt and verify with:
```
dotnet --version
```
It should print something like `8.0.xxx`.

---

## Step 2 — Install vJoy

1. Download **vJoy** from the jshafer817 fork: `https://github.com/jshafer817/vJoy/releases` — grab the latest `.exe` installer.
2. Run the installer.
3. After installing, open **"Configure vJoy"** from the Start menu and set **Device 1** to have:
   - Axes enabled: **X, Y, Z, Rx, Ry, Rz** (all six)
   - **Number of Buttons: 32** (or at least 23)
   - **POV Hat Switches: 1**, type **Continuous**
4. Click **Apply**.

---

## Step 3 — Copy vJoyInterface.dll

After installing vJoy, find this file on your system:
```
C:\Program Files\vJoy\x64\vJoyInterface.dll
```
You'll copy it alongside the built exe in Step 5.

---

## Step 4 — Build VaderLink

Open a Command Prompt in the repository folder (the one with `Vader5ProKeysticksLink.sln`) and run:
```
dotnet build src\VaderLink\VaderLink.csproj -c Release
```

The output exe will be at:
```
src\VaderLink\bin\Release\net8.0-windows\VaderLink.exe
```

---

## Step 5 — Place vJoyInterface.dll next to the exe

Copy `vJoyInterface.dll` from Step 3 into the same folder as `VaderLink.exe`:
```
src\VaderLink\bin\Release\net8.0-windows\
```

---

## Step 6 — Make sure Space Station is ready

Open **Flydigi Space Station**, connect the Vader 5 Pro, and confirm the toggle **"Allow third-party apps to take over mappings"** is enabled.

---

## Step 7 — Run VaderLink.exe

Double-click `VaderLink.exe`. A small icon should appear in your **system tray** (bottom-right near the clock). When the Vader 5 Pro connects successfully, the icon will change to indicate it's connected.

---

## Step 8 — Open Keysticks

Open Keysticks and look in its device list — you should see a **vJoy Device 1** listed. That's the virtual controller VaderLink is feeding. From there you can program all 23 buttons and 6 axes.

---

## If something goes wrong

- **"vJoy driver is not enabled"** — rerun the vJoy installer, then reconfigure Device 1 in Configure vJoy.
- **"No HID device found"** — make sure the controller is connected via USB dongle and that Space Station is running.
- **"No data received from enhanced mode"** — the Space Station toggle needs to be enabled.
- **Tray icon shows an error balloon** — the message in the balloon should describe the issue clearly.

---

The extra buttons (C, Z, M1–M4, LM, RM) should appear as buttons 12–19 in Keysticks.
