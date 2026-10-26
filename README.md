# G-Fan Control (TUF Fan Control)

Lightweight, G-Helper-inspired fan controller for **ASUS TUF Gaming** laptops (tested on **ASUS TUF Gaming F15 FX506LH**) where BIOS WMI custom fan curves (`0x00110024` / `0x00110025`) are locked or unsupported (`0xFFFFFFFE`), using ASUS's Embedded Controller **HealthyTable** hardware protocol over `\\.\AsusSAIO` (`IOCTL 0x80102070`, ports `0x25C` / `0x25D`).

## Features

- **4 Operating Modes:**
  - **Auto (BIOS):** Returns fan control to the Embedded Controller's built-in thermal curve (`0x31 = 0`).
  - **0 RPM Off:** Cuts PWM power to both CPU and GPU fans (`0x31 = 1, 0x35 = 0` -> `0 RPM`).
  - **Manual Speed:** Independent or linked `0%` to `100%` sliders for CPU Fan (`Fan 0`) and GPU Fan (`Fan 1`) with quick-step presets (`0%`, `25%`, `50%`, `75%`, `100%`).
  - **Custom Curve:** Interactive 8-point temperature-to-PWM curve (`30°C` to `100°C`) with temperature hysteresis and 4 presets (`0-RPM`, `Silent`, `Balanced`, `Turbo`).
- **Smooth fan transitions (anti-jank):**
  - The EC reports whole-degree steps, so raw readings and instant PWM writes made fans slam between 0% and 100%.
  - Sensor values are low-pass filtered for control decisions (cards still show the raw reading), and the safety override always uses the **raw** value so reaction time is never delayed.
  - PWM duty is slew-rate limited instead of jumping: rise is faster than fall, so fans spin up promptly but wind down gently.
  - Footprint pill **`Ramp: Smooth` / `Fast` / `Instant`** (Smooth = ~2.5 s up / 5 s down, Fast = 1 s / 2 s, Instant = direct write).
- **Hard thermal floor:** at **92 °C** the app always hands control back to EC Auto, even if the safety toggle is disabled or the limit slider is set lower. The safety limit slider is capped at 90 °C for the same reason.
- **Safety Thermal Cut-In Protection:** Configurable temperature limit (`70°C` to `90°C`, default `86°C`) that automatically hands fan control back to EC Auto if CPU or GPU reaches the threshold, with a `5°C` cooldown hysteresis.
- **Crash & Exit Safety:** Persistent `SYSTEM` worker monitors the UI process and automatically restores EC Auto (`0x31 = 0`) on exit or unexpected termination when `Auto on Exit` is enabled.
- **Light & Dark Mode:** One-click theme switch with Windows 10/11 DWM titlebar synchronization and system tray integration.

## Requirements

- Windows 10 / 11 (x64)
- ASUS System Control Interface driver (`AsusSAIO.sys` installed in DriverStore)
- Administrator privileges (automatically prompts via UAC and spawns its own `SYSTEM` worker for `\\.\AsusSAIO` access)

## Run or Build from Source

Run the precompiled `TufFanControl.exe` directly, or compile from source with zero external dependencies using the built-in .NET Framework x64 C# compiler:

```bat
build.bat
```
