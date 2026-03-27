# ESP32 Firmware

This folder contains all ESP32 firmware files for the LED animation system.

## Available Firmware

### 1. ESP32_Live_Controller
**Purpose:** Real-time Unity-to-ESP32 streaming  
**Use Case:** Development, testing, and live performances  
**Features:**
- UDP communication on port 8888
- 30 FPS animation streaming from Unity
- Real-time color updates
- Connection monitoring

**Upload this when:** You want to control LEDs directly from Unity in real-time

---

### 2. ESP32_Animation_Player
**Purpose:** Standalone animation playback  
**Use Case:** Installations without computer connection  
**Features:**
- Plays pre-exported animation files
- SD card storage support
- Playlist management
- Autonomous operation

**Upload this when:** You want the ESP32 to play animations independently

---

### 3. ESP32_Audio_SD_Test
**Purpose:** Audio hardware testing  
**Use Case:** Testing SD card and I2S audio output  
**Features:**
- SD card read/write testing
- I2S audio output validation
- Audio file playback testing
- Hardware diagnostics

**Upload this when:** You're setting up audio hardware for the first time

---

## Quick Start

1. Open the appropriate `.ino` file in Arduino IDE
2. Configure WiFi credentials (if needed)
3. Select your ESP32 board in Tools → Board
4. Upload to ESP32
5. Open Serial Monitor to see status messages

## Hardware Requirements

- ESP32 development board (any variant with WiFi)
- WS2812B LED strips
- 5V power supply (adequate for LED count)
- GPIO 19 for LED data (or modify in code)
- (Optional) SD card module for audio/animation storage
- (Optional) I2S DAC for audio output

## Documentation

See `Documentation/Setup/ESP32_SETUP_GUIDE.md` for detailed hardware setup instructions.
