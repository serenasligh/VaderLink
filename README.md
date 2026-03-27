# ***V A D E R L I N K***
A niche solution for the lads n ladies out there who love Keysticks and all the extra buttons on the Vader 5 Pro gamepad. 💖🎮

I'd say this project is a colaboration with Claude Sonnet 4.6, but the reality is that I just asked nicely and she wrote everything. The only thing I can really add to her work is an explanation for my original request.

I struggle with some chronic pain/stress that has made it difficult to sit in an upright posture continuously while using my computer. A solution I developed in the past is to remap a gamepad so it can be used to type, control a mouse, etc. Using a gamepad like this enables me to use a computer while lying down or curling up into positions that mitigate the discomfort. The **Flydigi Vader 5 Pro** is a highly featured controller with back paddles, extra MR/ML buttons, guide/function buttons, and a gyroscope: these features greatly expand the number of things I can do on a PC using only a controller. **Keysticks** is the only remapping program I've found that is versatile enough to build complex functionality into the controller, but unfortunately getting the Vader 5 Pro controller to connect to anything besides, you know, *video james* is impossible without writing custom software. So that's what I asked Sonnet for: custom software so I can keep innovating on my weird controller solution lmao. This project exposes all the inputs of the Vader 5 Pro controller in a way that allows Keysticks to read and remap the inputs from the extra buttons/features.

A personalized custom solution like this, which I probably never would have been able to develop on my own, leaves me with this incredibly strong urge to say *Thank You* to someone. I mean, I tried saying thank you to Sonnet though she just insisted that she's not a person, which, like, YEAH, I'm aware of that, but what else am I supposed to do? *Swallow my gratitude?* I guess I could just say thank you Anthropic, but that feels a bit like shouting at the sky in the town square or something. So... I think I'll say thank you here, just in general kind of ambient way, unspecifically, to no one in particular. It turns out the universe can actually be very gentle, kind sort of place, and my gratitude really can't be overstated. 💖 

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

Open **Flydigi Space Station**, connect the Vader 5 Pro, and confirm the toggle **"Allow third-party apps to take over mappings"** is enabled. You mentioned it already is — just double-checking it's still on!

---

## Step 7 — Run VaderLink.exe

Double-click `VaderLink.exe`. A small icon should appear in your **system tray** (bottom-right near the clock). When the Vader 5 Pro connects successfully, the icon will change to indicate it's connected.

---

## Step 8 — Open Keysticks

Open Keysticks and look in its device list — you should see a **vJoy Device 1** listed. That's the virtual controller VaderLink is feeding. From there you can program all 23 buttons and 6 axes just like you did with the Vader 4 Pro setup!

---

## If something goes wrong

- **"vJoy driver is not enabled"** — rerun the vJoy installer, then reconfigure Device 1 in Configure vJoy.
- **"No HID device found"** — make sure the controller is connected via USB dongle and that Space Station is running.
- **"No data received from enhanced mode"** — the Space Station toggle needs to be enabled.
- **Tray icon shows an error balloon** — the message in the balloon should describe the issue clearly.

---

Let me know what happens when you run it! The first test I'd suggest is just pressing each button on the controller and watching Keysticks to confirm the right button numbers light up. The extra buttons (C, Z, M1–M4, LM, RM) should appear as buttons 12–19 in Keysticks. If anything behaves unexpectedly, report back and we can dig into it.
