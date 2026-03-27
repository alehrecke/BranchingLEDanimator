# 🎵 Audio Hardware Setup Guide

## Overview
This guide covers the physical setup of SD card storage and I2S audio output components for the Branching LED Animator system. These components enable synchronized LED animations with high-quality stereo audio playback.

## 🔧 Required Components

### SD Card Reader Module
- **Type**: MicroSD card breakout board
- **Interface**: SPI communication
- **Voltage**: 3.3V compatible
- **Recommended**: Standard SPI SD card modules available from most electronics suppliers

### I2S Stereo DAC Module
- **Type**: Adafruit I2S 3W Class D Amplifier Breakout (MAX98357A) or similar
- **Interface**: I2S digital audio
- **Output**: Stereo analog audio (3W per channel)
- **Voltage**: 3.3V logic, 5V power for amplification

### MicroSD Card
- **Capacity**: 4GB minimum (32GB recommended)
- **Speed**: Class 10 or higher
- **Format**: FAT32
- **Usage**: Store audio files and animation data

### Speakers
- **Impedance**: 4-8 ohms
- **Power**: 3W per channel maximum
- **Type**: Small bookshelf speakers or powered monitors

## 📋 Wiring Diagram

### ESP32 Pin Assignments
```
SD Card Reader Module:
├── MISO → GPIO 19  ⚠️ CONFLICTS with existing LED data pin
├── MOSI → GPIO 23
├── SCK  → GPIO 18  
├── CS   → GPIO 5
├── VCC  → 3.3V
└── GND  → GND

I2S Stereo DAC Module:
├── DIN  → GPIO 22  (Digital Audio Data)
├── BCLK → GPIO 26  (Bit Clock)
├── WSEL → GPIO 25  (Word Select / L/R Clock)  
├── VCC  → 3.3V (logic) / 5V (amplifier power)
└── GND  → GND

Speakers:
├── Left Speaker  → DAC Left Output
└── Right Speaker → DAC Right Output
```

## 🚨 Critical Issue: GPIO Pin Conflict

### Problem
The SD card MISO pin (GPIO 19) conflicts with the existing LED data pin (GPIO 19) used in your current system.

### Solutions

#### Option A: Relocate LED Data Pin (Recommended)
**Move LED data from GPIO 19 to GPIO 2**
- **Pros**: Clean separation, no hardware conflicts
- **Cons**: Requires updating existing firmware
- **Implementation**: Change `#define LED_PIN 19` to `#define LED_PIN 2` in firmware

#### Option B: Software SPI for SD Card
**Use different pins for SD card with software SPI**
- **Pros**: No firmware changes needed for LED system
- **Cons**: Slower SD card performance
- **Implementation**: Use SoftwareSPI library with different pins

#### Option C: SPI Pin Sharing
**Share SPI bus between SD card and other devices**
- **Pros**: Efficient pin usage
- **Cons**: Complex implementation, potential timing issues
- **Implementation**: Requires careful SPI bus management

### Recommended Wiring (Option A)
```
UPDATED ESP32 Pin Assignments:
├── LED Strip Data → GPIO 2   (MOVED from GPIO 19)
├── SD MISO       → GPIO 19  (No longer conflicts)
├── SD MOSI       → GPIO 23
├── SD SCK        → GPIO 18
├── SD CS         → GPIO 5
├── I2S DIN       → GPIO 22
├── I2S BCLK      → GPIO 26
└── I2S WSEL      → GPIO 25
```

## 🔌 Physical Assembly Steps

### Step 1: Prepare Breadboard/PCB
1. Set up power rails (3.3V and GND)
2. Connect ESP32 to breadboard
3. Verify power connections with multimeter

### Step 2: Install SD Card Module
1. Connect SD card module to breadboard
2. Wire SPI connections as shown above
3. Insert formatted MicroSD card
4. Test with `ESP32_Audio_SD_Test.ino`

### Step 3: Install I2S DAC Module
1. Connect I2S DAC module to breadboard
2. Wire I2S connections as shown above
3. Connect speakers to DAC output
4. Test audio output with test tones

### Step 4: Update LED Wiring (if using Option A)
1. Disconnect LED data wire from GPIO 19
2. Connect LED data wire to GPIO 2
3. Update firmware with new pin assignment
4. Test LED functionality

### Step 5: Integration Testing
1. Upload `ESP32_Audio_SD_Test.ino`
2. Run hardware validation tests
3. Test audio playback with sample files
4. Verify no interference between systems

## 🧪 Testing Procedure

### Initial Hardware Test
```cpp
// Upload ESP32_Audio_SD_Test.ino and run these commands:
sd      // Test SD card mounting and file operations
tone    // Test I2S audio with 440Hz tone
wav     // Test WAV file playback (requires test.wav on SD card)
status  // Check all hardware initialization
```

### Expected Results
- ✅ SD card mounts successfully
- ✅ Audio plays through speakers clearly
- ✅ No interference with existing LED system
- ✅ All GPIO pins function as expected

## 📁 SD Card Setup

### Format SD Card
1. Format as FAT32 using computer
2. Create directory structure:
   ```
   /audio/
     /test/
       test.wav          ← Test audio file
     /animations/        ← Future animation audio files
     /playlists/         ← Future playlist files
   ```

### Test Audio File
Create or download a test WAV file with these specifications:
- **Sample Rate**: 44.1 kHz
- **Bit Depth**: 16-bit
- **Channels**: Stereo
- **Duration**: 10-30 seconds
- **Name**: `test.wav`
- **Location**: Root directory of SD card

## ⚡ Power Considerations

### Power Requirements
- **ESP32**: ~240mA (WiFi active)
- **SD Card**: ~100mA (during read/write)
- **I2S DAC**: ~50mA (logic) + speaker power
- **LED Strip**: Existing power supply (unchanged)

### Power Supply Recommendations
- **5V 3A** minimum for complete system
- Separate power for high-current LED strips
- Clean power filtering for audio quality
- Consider USB power limitations during development

## 🔧 Troubleshooting

### SD Card Issues
- **Not mounting**: Check wiring, try different SD card
- **Slow performance**: Use Class 10 or better SD card
- **File errors**: Reformat SD card as FAT32

### Audio Issues
- **No sound**: Check speaker connections, volume levels
- **Distorted audio**: Check power supply quality, ground connections
- **Mono output**: Verify stereo wiring, test with known stereo file

### Pin Conflict Issues
- **LEDs not working**: Verify new GPIO pin assignment in firmware
- **SD card interference**: Ensure clean SPI bus connections
- **System instability**: Check for ground loops, power supply noise

## 📈 Performance Expectations

### Audio Quality
- **Frequency Response**: 20Hz - 20kHz (limited by DAC and speakers)
- **Dynamic Range**: 16-bit (96dB theoretical)
- **THD**: <1% with quality DAC module
- **Latency**: ~23ms (1024 sample buffer at 44.1kHz)

### System Performance
- **LED Frame Rate**: Maintained at 30 FPS during audio playback
- **Audio Playback**: Smooth, no dropouts with Class 10 SD card
- **File Access**: ~1-2 second startup time for audio files
- **Memory Usage**: ~64KB RAM for audio buffers

## 🎯 Next Steps

After completing hardware setup:

1. **Test Hardware**: Run `ESP32_Audio_SD_Test.ino` validation
2. **Resolve Conflicts**: Implement chosen GPIO pin solution
3. **Create Test Content**: Prepare sample audio files
4. **Unity Integration**: Begin Unity audio export system development
5. **Synchronization**: Develop LED+audio timing coordination

---

**With this hardware setup complete, you'll have a foundation for creating immersive LED+audio experiences that can be choreographed in Unity and played back standalone on the ESP32.** 🎵✨
