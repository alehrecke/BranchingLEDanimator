# 🔌 ESP32 LED Controller Setup Guide

## 📋 **Hardware Requirements**

### **Components:**
- ✅ **ESP32 development board** (any ESP32 with WiFi)
- ✅ **WS2812B LED strips** (configurable LED count - you set manually)
- ✅ **5V power supply** (adequate for your total LED count)
- ✅ **Jumper wires** for branching connections (if needed)
- ✅ **Level shifter** (optional but recommended for data signal)

### **Current Working Configuration:**
- **GPIO Pin:** 19 (configurable in firmware)
- **Total LEDs:** User configurable (e.g., 99 LEDs = 3 strips × 33 each)
- **Logical Strips:** User defines strip count and LED counts
- **Protocol:** Custom UDP format on port 8888
- **Firmware:** `ESP32_Live_Controller.ino` for Unity connectivity

## 🔧 **Physical Wiring**

### **ESP32 Connections:**
```
ESP32 GPIO 19 → Level Shifter → LED Strip Data Input
ESP32 GND → Common Ground
5V PSU + → LED Strip VCC  
5V PSU - → Common Ground
```

### **LED Strip Layout (Example for 99 LEDs):**
```
Physical Strip: [Strip0: 0-32][Strip1: 33-65][Strip2: 66-98]
                     ↓            ↓            ↓
Unity Mapping:   33 LEDs      33 LEDs      33 LEDs
                     ↓            ↓            ↓  
Configuration:  Forward      Forward      Reverse (if needed)
```

### **Manual Configuration Required:**
- **You set LED counts** per strip in Unity LEDCircuitMapper
- **You set wiring directions** (Forward/Reverse) based on your physical setup
- **You set strip order** based on your data flow sequence
- **Flexible setup** - works with any LED count configuration

## 💾 **Software Setup**

### **Step 1: ESP32 Firmware**
1. **Upload `ESP32_Live_Controller.ino`** to your ESP32
2. **Update WiFi credentials** in the firmware:
   ```cpp
   const char* ssid = "YourWiFiNetwork";
   const char* password = "YourPassword";
   ```
3. **Check Serial Monitor** for ESP32's IP address after upload

### **Step 2: Unity System Setup**
1. **Create complete system**: `Tools → LED Animation System → Create Complete System GameObject`
2. **Import geometry**: Right-click LEDGraphManager → "Import Grasshopper Data"
3. **Configure ESP32Communicator:**
   - Set ESP32 IP address (from ESP32 Serial Monitor)
   - Port should be 8888 (matches firmware)
   - Ensure "Use Custom Protocol" is checked

### **Step 3: Manual LED Configuration**
1. **In LEDCircuitMapper inspector**, configure your physical setup:
   ```
   Strip 0: [Your LED count], [Forward/Reverse], Order 0
   Strip 1: [Your LED count], [Forward/Reverse], Order 1
   Strip 2: [Your LED count], [Forward/Reverse], Order 2
   ```
2. **Right-click LEDCircuitMapper** → **"Create LED Mapping"**
3. **Right-click ESP32Communicator** → **"Refresh Component References"**

### **Step 4: Test Connection**
1. **Right-click ESP32Communicator** → **"Connect to ESP32"**
2. **Check console** for "Connected to ESP32 at [IP]:8888"
3. **Right-click ESP32Communicator** → **"Send Test Pattern - Rainbow"**

## 🧪 **Testing Steps**

### **1. ESP32 Hardware Test:**
1. **Upload firmware** → ESP32 should show startup LED pattern
2. **Check Serial Monitor** → Should show:
   ```
   WiFi connected!
   IP address: 192.168.x.x
   UDP server started on port 8888
   ```

### **2. Unity Component Test:**
1. **Right-click ESP32Communicator** → **"Refresh Component References"**
   - Console should show: LEDCircuitMapper: ✅ Found
   - Console should show: LEDGraphManager: ✅ Found

2. **Right-click LEDCircuitMapper** → **"Create LED Mapping"**
   - Should map Unity nodes to your configured LED strips
   - Console shows: "Mapping Complete: True"
   - Console shows your total LED count and strip configuration

### **3. ESP32 Connection Test:**
1. **Right-click ESP32Communicator** → **"Connect to ESP32"**
   - Console: "Connected to ESP32 at [IP]:8888"
   - Console: "Sent LED configuration to ESP32"

2. **Right-click ESP32Communicator** → **"Show Stats"**
   - Should show: Circuit Mapper Status: ✅ Complete
   - Should show your LED counts and strip configuration

### **4. Physical LED Test:**
1. **Test patterns:**
   - **"Send Test Pattern - Rainbow"** → Animated rainbow on LEDs
   - **"Send Test Pattern - White"** → All LEDs white
   - **"Send Test Pattern - Off"** → All LEDs off

2. **Live animation streaming:**
   - Unity animations should display on physical LEDs in real-time!
   - Change animation in LEDAnimationSystem → LEDs update instantly
   - 30 FPS smooth animation playback

## 🔍 **Troubleshooting**

### **ESP32 Issues:**
- **No WiFi connection:** Check SSID/password in firmware, router settings
- **LEDs not lighting:** Check power supply capacity, GPIO 19 connection
- **Random colors:** Check level shifter, common ground connections
- **Port issues:** Ensure firmware uses port 8888 (matches Unity)

### **Unity Component Issues:**
- **"Circuit Mapper Status: ❌ Incomplete":** Right-click ESP32Communicator → "Refresh Component References"
- **"No geometry data":** Right-click LEDGraphManager → "Import Grasshopper Data"
- **"No mapping":** Configure LED strips in LEDCircuitMapper, then "Create LED Mapping"

### **Connection Issues:**
- **"No ESP32 connection":** Check ESP32 IP address in ESP32Communicator
- **"Connection timeout":** Ensure Unity and ESP32 on same WiFi network
- **"Packets not received":** Check firewall settings, ESP32 serial monitor

### **LED Configuration Issues:**
- **Wrong LED count:** Manually set LED counts per strip in LEDCircuitMapper
- **Wrong colors/positions:** Check Forward/Reverse direction settings
- **Partial lighting:** Verify total LED count matches physical setup

## 📊 **Expected Results**

### **Physical LED Layout (Example):**
```
Unity Animation → Manual LED Configuration → Physical LEDs

Unity Nodes (157)    → Strip 0 (33 LEDs) → LEDs 0-32
                   → Strip 1 (33 LEDs) → LEDs 33-65
                   → Strip 2 (33 LEDs) → LEDs 66-98
                   
(Your configuration may differ based on your physical setup)
```

### **Animation Behavior:**
- ✅ **Real-time streaming:** Unity animations appear on physical LEDs instantly
- ✅ **30 FPS performance:** Smooth animation playback
- ✅ **Configurable mapping:** Works with any LED strip configuration
- ✅ **Test patterns:** Rainbow, white, off patterns for validation
- ✅ **Live updates:** Change animations in Unity → LEDs update immediately

### **Success Indicators:**
1. **ESP32 Serial Monitor:** 
   ```
   UDP server started on port 8888
   Received packet from Unity: 192.168.x.x
   LED data received: 99 LEDs
   ```
2. **Unity Console:** 
   ```
   Circuit Mapper Status: ✅ Complete
   Connected to ESP32 at [IP]:8888
   Sent LED configuration: 99 LEDs (3 strips)
   ```
3. **Physical LEDs:** Display animations matching Unity Scene view
4. **Real-time response:** Animation changes update LEDs within 1-2 frames

## 🎯 **Final Result**

**Your Unity LED animations will stream in real-time to your physical LEDs with full configurability:**

- ✅ **Manual LED configuration** - set LED counts to match your exact hardware
- ✅ **Real-time streaming** - 30 FPS Unity-to-ESP32 animation playback
- ✅ **Complete testing suite** - validate every component and connection
- ✅ **Professional monitoring** - real-time status and diagnostics
- ✅ **Future-ready** - prepared for playlist system and sound synchronization

**The system is designed for production LED installations while maintaining ease of use!** 🎆✨
