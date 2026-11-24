# Download and play Baby Clash

These ready-to-run packages contain the tested game from commit `9036c1b` (November 18, 2026).

## Windows 10 or 11 (64-bit Intel/AMD)

**[Download the Windows installer](https://github.com/baguixgroup/baby-clash/raw/refs/heads/main/Installers/BabyClash-9036c1b-Setup.exe)** — 38 MB

1. Download the file and double-click it.
2. Follow the setup wizard, then launch **Baby Clash** from the Start menu.

The test build is unsigned. If SmartScreen blocks it, verify that you downloaded it from this repository before choosing **More info → Run anyway**. Uninstall through Windows **Settings → Apps**.

The tested Intel HD Graphics 530 PC averaged 4 FPS at Ultra settings.

## Raspberry Pi OS (64-bit)

**[Download the Raspberry Pi package](https://github.com/baguixgroup/baby-clash/raw/refs/heads/main/Installers/BabyClash-9036c1b-RaspberryPi.zip)** — 20 MB

1. Extract the ZIP on your Pi.
2. Open a terminal in the extracted **RaspberryPi** folder.
3. Run `python3 play-on-pi.py`.
4. Keep the terminal open while playing in Chromium. Press Ctrl+C in the terminal when finished.

Requires Python 3 and Chromium. The Pi 4 completed a three-round test on its V3D GPU, averaging 34 FPS in headless Chromium.

## Controls

| Player | Move | Grab / throw | Dash |
| --- | --- | --- | --- |
| Baby 1 | WASD | Space | Left Shift |
| Baby 2 | Arrow keys | Enter | Right Shift |

Press Esc to pause. Standard gamepads also work.

## Verify downloads

SHA-256 checksums:

```text
6cb3a7d671f95a8ae02cc7d12ed5386289e8288f22b1c50a3e81c450ccd071e1  BabyClash-9036c1b-Setup.exe
952e378c79791f6c99e356e1171db5982a61517d33c51360b496ba3841a32cbe  BabyClash-9036c1b-RaspberryPi.zip
```

This repository is private. People downloading these packages need repository access.
