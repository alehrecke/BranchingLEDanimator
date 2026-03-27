# ESP32 Unified LED Controller Setup Guide

## 🚀 **What's New in the Unified Version**

The `ESP32_Unified_LED_Controller.ino` consolidates all functionality from the previous versions:

- ✅ **OTA Updates**: Update code over WiFi without USB cable
- ✅ **Web Monitor**: Real-time serial monitor and LED testing in browser
- ✅ **Unity Connectivity**: Fixed port 8888 to match Unity ESP32Communicator
- ✅ **Dual Protocol Support**: Handles both legacy and new packet formats
- ✅ **Enhanced Monitoring**: Statistics, heartbeat, connection status
- ✅ **Serial Commands**: Control LEDs via Serial Monitor
- ✅ **Robust Error Handling**: Better connection recovery and debugging

## 📋 **Quick Setup Steps**

### 1. **Hardware Setup**
```
ESP32 Development Board
├── GPIO 19 → LED Strip Data (through level shifter recommended)
├── 5V → LED Strip Power
└── GND → Common Ground (ESP32, PSU, LED Strip)

LED Configuration:
- Single continuous WS2812B strip
- 99-150 LEDs total (configurable in code)
- 3 segments of 33 LEDs each (matches your setup)
```

### 2. **Software Upload**
1. **Open Arduino IDE**
2. **Load**: `ESP32_Unified_LED_Controller.ino`
3. **Configure WiFi** (lines 31-32):
   ```cpp
   const char* ssid = "YourWiFiName";        // Your WiFi network
   const char* password = "YourPassword";    // Your WiFi password
   ```
4. **Upload** to ESP32

### 3. **First Boot**
After upload, the ESP32 will:
- ✅ Connect to WiFi (blue dots animation)
- ✅ Show startup pattern (segment test)
- ✅ Display IP address in Serial Monitor
- ✅ Start web server and OTA

### 4. **Unity Configuration**
In Unity ESP32Communicator:
- **ESP32 IP Address**: Use the IP shown in Serial Monitor
- **ESP32 Port**: `8888` (matches unified controller)
- **Enable**: "Use Custom Protocol"

## 🌐 **Web Interface Features**

Access via: `http://[ESP32_IP_ADDRESS]`

### **Real-time Monitoring**
- Live serial output
- Connection status with Unity
- Frame rate and packet statistics
- System information (uptime, memory, etc.)

### **LED Testing Controls**
- **Test All LEDs**: White pattern on all LEDs
- **Rainbow Test**: Animated rainbow pattern
- **Test Segments**: Individual segment testing (R→G→B)
- **LEDs Off**: Turn off all LEDs
- **Clear Console**: Clear web serial buffer

### **Status Dashboard**
- IP Address and WiFi network
- Unity connection status (✅/❌)
- Real-time FPS from Unity
- Total packets received
- LED count and segment info

## 🔄 **OTA (Over-The-Air) Updates**

### **Arduino IDE Method**
1. **Tools** → **Port** → **ESP32_LED_Controller at [IP]**
2. **Upload** normally - no USB cable needed!
3. Progress shown on LEDs (blue bar)

### **Web Upload Method**
- Visit: `http://[ESP32_IP]/update`
- Upload `.bin` file directly

## 📡 **Unity Connectivity**

### **Connection Process**
1. ESP32 starts UDP server on port **8888**
2. Unity ESP32Communicator connects to ESP32 IP:8888
3. Automatic protocol detection (legacy vs new format)
4. Real-time LED data streaming

### **Supported Protocols**
- **Legacy Protocol**: `[0xAA][CMD][STRIP][DATA]...`
- **New Protocol**: Structured packets with headers
- **Auto-detection**: Works with both formats seamlessly

### **Troubleshooting Unity Connection**
- ❌ **"No connection"**: Check ESP32 IP in Unity
- ❌ **"Partial lighting"**: Verify LED count matches Unity mapping
- ❌ **"No data"**: Enable "Use Custom Protocol" in Unity
- ❌ **"Timeout"**: Check firewall/network settings

## 🛠️ **Serial Commands**

Type in Serial Monitor (115200 baud):
- `status` - Show detailed system information
- `rainbow` - Apply rainbow pattern
- `white` - Turn all LEDs white
- `off` - Turn off all LEDs
- `test` - Test all segments individually
- `restart` - Restart the ESP32

## 📊 **Monitoring & Statistics**

### **Serial Output**
- Connection events
- Packet reception logs
- Error messages and warnings
- Performance statistics every 10 seconds

### **Web Dashboard**
- Real-time status updates every 2 seconds
- Connection health monitoring
- Frame rate from Unity animations
- Memory usage and system health

### **Visual Indicators**
- **Heartbeat**: First LED flashes green every 2 seconds
- **WiFi Connecting**: Blue dots animation
- **WiFi Error**: Red flashing
- **OTA Progress**: Blue progress bar
- **Connection**: Blue flash on Unity connect

## 🔧 **Configuration Options**

### **LED Configuration** (lines 35-42)
```cpp
#define LED_PIN 19                // GPIO pin for LED data
#define MAX_LEDS 150             // Maximum LEDs supported
#define LEDS_PER_SEGMENT 33      // LEDs per segment
#define NUM_SEGMENTS 3           // Number of segments
```

### **Network Configuration** (lines 44-46)
```cpp
#define UDP_PORT 8888            // Unity communication port
#define WEB_PORT 80              // Web server port
```

### **Performance Tuning**
- Brightness: `currentBrightness = 128` (50% to prevent power issues)
- Buffer sizes: Configurable for different LED counts
- Update rates: Automatic based on Unity frame rate

## 🚨 **Error Handling**

### **WiFi Issues**
- **Connection timeout**: Red flashing LEDs
- **Lost connection**: Automatic reconnection attempts
- **Weak signal**: Shown in web interface

### **Unity Issues**
- **Connection timeout**: 30-second timeout with notification
- **Invalid packets**: Logged with details
- **Protocol errors**: Auto-detection and fallback

### **Hardware Issues**
- **LED problems**: Individual segment testing
- **Memory issues**: Heap monitoring and warnings
- **Power issues**: Brightness limiting

## 📈 **Performance Expectations**

### **Typical Performance**
- **Frame Rate**: Up to 30 FPS from Unity
- **Latency**: <50ms from Unity to LEDs
- **Memory Usage**: ~50KB heap usage
- **WiFi Range**: Standard 2.4GHz range

### **Optimizations**
- Large packet buffers for smooth animation
- Efficient LED updates with FastLED
- Minimal processing overhead
- Smart packet parsing

## 🎯 **Next Steps**

1. **Upload** the unified controller
2. **Connect** Unity to the ESP32 IP:8888
3. **Test** using web interface
4. **Monitor** via web dashboard
5. **Update** over WiFi using OTA when needed

## 🆘 **Support**

### **Debug Information**
- Check Serial Monitor output
- Use web interface for real-time monitoring
- Enable debug logging in Unity
- Test individual segments via web interface

### **Common Solutions**
- **Restart ESP32**: Use `restart` serial command
- **Check connections**: Physical LED wiring
- **Verify network**: Same WiFi network for Unity and ESP32
- **Update code**: Use OTA for quick updates

---

**The unified controller provides the most robust and feature-complete solution for your Unity LED animation project!**
