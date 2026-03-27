/*
 * ESP32 Live Controller for Unity Branching LED Animator
 * Optimized for live Unity connectivity and real-time LED control
 * 
 * Features:
 * - WiFi OTA updates (Over-The-Air programming)
 * - Web-based serial monitor and LED testing
 * - Unity UDP communication on port 8888 (matches Unity ESP32Communicator)
 * - Support for both legacy protocol and new packet format
 * - Continuous WS2812B LED strip control
 * - Real-time statistics and monitoring
 * 
 * MEMORY OPTIMIZED: No animation storage or playlist functionality
 * Use ESP32_Animation_Player.ino for standalone animation playback
 * 
 * Hardware Setup:
 * - ESP32 development board
 * - Single WS2812B LED strip (continuous, ~99-150 LEDs total)
 * - GPIO 19 → LED strip data input (through level shifter recommended)
 * - 5V power supply (adequate for your LED count)
 * - GND → Common ground (ESP32, PSU, LED strip)
 * 
 * Unity Configuration:
 * - ESP32Communicator should connect to port 8888
 * - Set ESP32 IP address in Unity to match this device's IP
 * - Enable "Use Custom Protocol" in Unity
 */

#include <WiFi.h>
#include <WiFiUdp.h>
#include <WebServer.h>
#include <ArduinoOTA.h>
#include <FastLED.h>
#include <Preferences.h>

// ===================== CONFIGURATION =====================
// WiFi Configuration
const char* ssid = "Pneuhaus";           // Your WiFi network name
const char* password = "Pneuhaus310";    // Your WiFi password

// LED Configuration
#define LED_PIN 19
#define MAX_LEDS 1000             // Maximum LEDs supported (increased for larger setups)
#define LED_TYPE WS2812B
#define COLOR_ORDER RGB

// Dynamic LED Configuration (updated by Unity)
struct LEDConfig {
  int totalLEDs = 100;          // Default for simpler circuits - Unity will override
  int numSegments = 1;          // Single strip by default
  int ledsPerSegment = 100;     // Same as totalLEDs for single strip
  bool isConfigured = false;    // Will be set to true when Unity sends config
  String lastConfigSource = "default"; // Track where config came from
  
  // Individual strip/segment LED counts (for accurate mapping visualization)
  int stripLEDs[10];            // LED count for each strip/segment (max 10 segments)
  int stripStartIndices[10];    // Starting LED index for each strip
};

LEDConfig ledConfig;

// Network Configuration
#define UDP_PORT 8888             // Main port (matches Unity ESP32Communicator)
#define WEB_PORT 80               // Web server port

// Protocol Configuration - Support both formats
#define PACKET_START_MARKER 0xAA  // Legacy protocol marker
#define COMMAND_TEST_PATTERN 0x03
#define COMMAND_LED_DATA 0x01
#define COMMAND_CONNECTION 0x02
#define COMMAND_LED_CONFIG 0x05          // LED configuration packet

// New packet format
enum PacketType {
  Connection = 0,
  LEDData = 1,
  Configuration = 2,
  Heartbeat = 3
};

enum LEDDataFormat {
  RGB24 = 0,
  RGBW32 = 1,
  RGB565 = 2,
  HSV24 = 3
};

struct PacketHeader {
  uint8_t packetType;
  uint32_t timestamp;
  uint8_t stripCount;
  uint8_t dataFormat;
  uint8_t reserved;
};

// ==================== GLOBAL VARIABLES ====================
// LED Array
CRGB leds[MAX_LEDS];

// Segment tracking
struct Segment {
  int startLED;
  int endLED;
  int activeCount;
  bool isActive;
};
Segment segments[10]; // Max 10 segments supported

// Network components
WiFiUDP udp;
WebServer server(WEB_PORT);

// Configuration storage
Preferences preferences;
IPAddress unityIP;
bool unityConnected = false;

// Web Serial Monitor (reduced buffer for memory optimization)
String serialBuffer = "";
const int MAX_SERIAL_BUFFER = 2000; // Reduced from 5000

// Statistics
unsigned long packetsReceived = 0;
unsigned long bytesReceived = 0;
unsigned long lastPacketTime = 0;
unsigned long lastStatsTime = 0;
float fps = 0;
int totalActiveLEDs = 0;

// LED state
uint8_t currentBrightness = 255;  // Full brightness by default
bool ledsEnabled = true;

// ======================= SETUP =======================
void setup() {
  Serial.begin(115200);
  Serial.println("\n🚀 ESP32 Live Controller Starting...");
  Serial.println("Features: OTA + Web Monitor + Unity Live View + Persistent Config");
  
  // Load saved LED configuration
  loadLEDConfiguration();
  
  // Initialize segments
  setupSegments();
  
  // Initialize LEDs
  setupLEDs();
  
  // Connect to WiFi
  setupWiFi();
  
  // Initialize UDP
  setupUDP();
  
  // Initialize Web Server
  setupWebServer();
  
  // Initialize OTA
  setupOTA();
  
  // Show startup pattern
  showStartupPattern();
  
  // Show configuration status
  if (!ledConfig.isConfigured) {
    webSerial("⚙️ Using default LED configuration (100 LEDs, 1 strip)\n");
    webSerial("💡 Connect Unity to send proper LED configuration for complex setups\n");
  } else {
    webSerial("✅ LED configuration: " + String(ledConfig.totalLEDs) + " LEDs, " + String(ledConfig.numSegments) + " strips (source: " + ledConfig.lastConfigSource + ")\n");
  }
  
  webSerial("✅ ESP32 Live Controller Ready!\n");
  webSerial("📡 UDP listening on port " + String(UDP_PORT) + "\n");
  webSerial("🌐 Web monitor at http://" + WiFi.localIP().toString() + "\n");
  webSerial("🔄 OTA enabled as 'ESP32_Live_Controller'\n");
  webSerial("💡 For standalone animations, use ESP32_Animation_Player\n");
}

// ======================= MAIN LOOP =======================
void loop() {
  // Handle OTA updates (highest priority)
  ArduinoOTA.handle();
  
  // Handle web server requests
  server.handleClient();
  
  // Handle UDP packets from Unity
  handleUDPPackets();
  
  // Update statistics and monitoring
  updateStatistics();
  
  // Show heartbeat (every 2 seconds)
  static unsigned long lastHeartbeat = 0;
  if (millis() - lastHeartbeat > 2000) {
    heartbeat();
    lastHeartbeat = millis();
  }
  
  // Check for lost Unity connection
  if (unityConnected && (millis() - lastPacketTime > 30000)) {
    webSerial("⚠️ Lost connection to Unity (30s timeout)\n");
    unityConnected = false;
  }
  
  // Handle serial commands
  handleSerialCommands();
  
  // Small delay to prevent watchdog issues
  delay(1);
}

// ===================== SETUP FUNCTIONS =====================
void setupSegments() {
  webSerial("📦 Setting up LED segments...\n");
  updateSegmentConfiguration();
}

void updateSegmentConfiguration() {
  webSerial("🔧 Updating segment configuration...\n");
  
  for (int i = 0; i < ledConfig.numSegments && i < 10; i++) { // Max 10 segments for safety
    segments[i].startLED = i * ledConfig.ledsPerSegment;
    segments[i].endLED = segments[i].startLED + ledConfig.ledsPerSegment - 1;
    
    // Ensure we don't exceed total LEDs
    if (segments[i].endLED >= ledConfig.totalLEDs) {
      segments[i].endLED = ledConfig.totalLEDs - 1;
    }
    
    segments[i].activeCount = segments[i].endLED - segments[i].startLED + 1;
    segments[i].isActive = false;
    
    Serial.printf("  - Segment %d: LEDs %d-%d (%d LEDs)\n", i, segments[i].startLED, segments[i].endLED, segments[i].activeCount);
  }
  
  totalActiveLEDs = ledConfig.totalLEDs;
  Serial.printf("✅ Total LED positions: %d\n", totalActiveLEDs);
}

void setupLEDs() {
  webSerial("💡 Initializing WS2812B LED strip...\n");
  
  // Initialize FastLED with maximum capacity (will use actual count from config)
  FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, MAX_LEDS);
  FastLED.setBrightness(currentBrightness);
  FastLED.setCorrection(TypicalLEDStrip);
  
  // Clear all LEDs
  FastLED.clear();
  FastLED.show();
  
  webSerial("✅ FastLED initialized on GPIO " + String(LED_PIN) + " (capacity: " + String(MAX_LEDS) + ")\n");
  webSerial("🔧 Using LED config: " + String(ledConfig.totalLEDs) + " total, " + String(ledConfig.numSegments) + " segments\n");
  webSerial("📡 LED configuration will be updated when Unity sends config packet\n");
}

void setupWiFi() {
  webSerial("📶 Connecting to WiFi: " + String(ssid) + "\n");
  
  // Show WiFi connecting pattern
  fill_solid(leds, totalActiveLEDs, CRGB::Blue);
  FastLED.setBrightness(64);
  FastLED.show();
  
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);
  
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 30) {
    delay(500);
    Serial.print(".");
    
    // Animate dots during connection
    for (int i = 0; i < 3; i++) {
      leds[i] = (attempts % 3 == i) ? CRGB::White : CRGB::Blue;
    }
    FastLED.show();
    attempts++;
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    webSerial("\n✅ WiFi connected!\n");
    webSerial("📍 IP address: " + WiFi.localIP().toString() + "\n");
    webSerial("📊 Signal strength: " + String(WiFi.RSSI()) + " dBm\n");
    
    // Show success pattern (green)
    fill_solid(leds, totalActiveLEDs, CRGB::Green);
    FastLED.show();
    delay(1000);
  } else {
    webSerial("\n❌ WiFi connection failed!\n");
    
    // Show error pattern (red flashing)
    for (int i = 0; i < 5; i++) {
      fill_solid(leds, totalActiveLEDs, CRGB::Red);
      FastLED.show();
      delay(200);
      FastLED.clear();
      FastLED.show();
      delay(200);
    }
  }
  
  // Clear LEDs and restore brightness
  FastLED.clear();
  FastLED.setBrightness(currentBrightness);
  FastLED.show();
}

void setupUDP() {
  webSerial("📡 Starting UDP server on port " + String(UDP_PORT) + "...\n");
  
  if (udp.begin(UDP_PORT)) {
    webSerial("✅ UDP server started successfully\n");
  } else {
    webSerial("❌ UDP server failed to start\n");
  }
}

void setupWebServer() {
  webSerial("🌐 Starting web server on port " + String(WEB_PORT) + "...\n");
  
  // Root page - dynamic interface based on Unity config
  server.on("/", HTTP_GET, []() {
    String html = generateDynamicStatusPage();
    // Add strong cache-busting headers
    server.sendHeader("Cache-Control", "no-cache, no-store, must-revalidate");
    server.sendHeader("Pragma", "no-cache");
    server.sendHeader("Expires", "0");
    server.sendHeader("Last-Modified", "Thu, 01 Jan 1970 00:00:00 GMT");
    server.send(200, "text/html", html);
  });
  
  // Serial monitor endpoint
  server.on("/serial", HTTP_GET, []() {
    server.send(200, "text/plain", serialBuffer);
  });
  
  // Clear serial buffer
  server.on("/clear", HTTP_GET, []() {
    serialBuffer = "";
    server.send(200, "text/plain", "Serial buffer cleared");
  });
  
  // LED test endpoints
  server.on("/test/all", HTTP_GET, []() {
    fill_solid(leds, totalActiveLEDs, CRGB::White);
    FastLED.show();
    server.send(200, "text/plain", "All LEDs: White");
  });
  
  server.on("/test/rainbow", HTTP_GET, []() {
    rainbowPattern();
    FastLED.show();
    server.send(200, "text/plain", "Rainbow pattern applied");
  });
  
  server.on("/test/segments", HTTP_GET, []() {
    testAllSegments();
    server.send(200, "text/plain", "Segment test complete");
  });
  
  server.on("/leds/off", HTTP_GET, []() {
    FastLED.clear();
    FastLED.show();
    server.send(200, "text/plain", "All LEDs: Off");
  });
  
  // Brightness control endpoints
  server.on("/brightness/max", HTTP_GET, []() {
    currentBrightness = 255;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: MAX (255/255)");
  });
  
  server.on("/brightness/high", HTTP_GET, []() {
    currentBrightness = 200;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: HIGH (200/255)");
  });
  
  server.on("/brightness/medium", HTTP_GET, []() {
    currentBrightness = 128;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: MEDIUM (128/255)");
  });
  
  server.on("/brightness/low", HTTP_GET, []() {
    currentBrightness = 64;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: LOW (64/255)");
  });
  
  server.on("/api/brightness", HTTP_GET, []() {
    if (server.hasArg("value")) {
      int brightness = server.arg("value").toInt();
      if (brightness >= 0 && brightness <= 255) {
        currentBrightness = brightness;
        FastLED.setBrightness(currentBrightness);
        FastLED.show();
        server.send(200, "text/plain", "Brightness set to: " + String(brightness) + "/255");
      } else {
        server.send(400, "text/plain", "Invalid brightness value. Range: 0-255");
      }
    } else {
      server.send(200, "text/plain", "Current brightness: " + String(currentBrightness) + "/255");
    }
  });
  
  // LED debugging endpoints (matching Animation Player)
  server.on("/api/led/color_test", HTTP_GET, []() {
    // Test individual strip mapping with different colors
    CRGB colors[] = {CRGB::Red, CRGB::Green, CRGB::Blue, CRGB::Yellow, CRGB::Magenta, 
                     CRGB::Cyan, CRGB::Orange, CRGB::Purple, CRGB::Pink, CRGB::White};
    
    FastLED.clear();
    
    if (ledConfig.isConfigured) {
      String responseMsg = "Strip mapping: ";
      for (int strip = 0; strip < ledConfig.numSegments && strip < 10; strip++) {
        int startLED = ledConfig.stripStartIndices[strip];
        int stripLEDs = ledConfig.stripLEDs[strip];
        CRGB color = colors[strip % 10];
        
        // Light up this strip
        for (int i = startLED; i < startLED + stripLEDs; i++) {
          if (i < ledConfig.totalLEDs) {
            leds[i] = color;
          }
        }
        
        // Add to response message
        if (strip > 0) responseMsg += " | ";
        responseMsg += "Strip" + String(strip) + "(" + String(stripLEDs) + ")";
      }
      
      FastLED.show();
      server.send(200, "text/plain", responseMsg + " - Total: " + String(ledConfig.totalLEDs) + " LEDs");
    } else {
      server.send(400, "text/plain", "LED configuration not received from Unity");
    }
  });
  
  server.on("/api/led/single", HTTP_GET, []() {
    if (!server.hasArg("strip") || !server.hasArg("color")) {
      server.send(400, "text/plain", "Missing parameters. Use: /api/led/single?strip=0&color=red");
      return;
    }
    
    int stripIndex = server.arg("strip").toInt();
    String colorStr = server.arg("color");
    
    if (stripIndex < 0 || stripIndex >= ledConfig.numSegments) {
      server.send(400, "text/plain", "Invalid strip index. Range: 0-" + String(ledConfig.numSegments - 1));
      return;
    }
    
    FastLED.clear();
    
    CRGB color = CRGB::White;
    if (colorStr == "red") color = CRGB::Red;
    else if (colorStr == "green") color = CRGB::Green;
    else if (colorStr == "blue") color = CRGB::Blue;
    else if (colorStr == "yellow") color = CRGB::Yellow;
    else if (colorStr == "cyan") color = CRGB::Cyan;
    else if (colorStr == "magenta") color = CRGB::Magenta;
    
    int startLED = ledConfig.stripStartIndices[stripIndex];
    int stripLEDs = ledConfig.stripLEDs[stripIndex];
    
    for (int i = startLED; i < startLED + stripLEDs; i++) {
      if (i < ledConfig.totalLEDs) {
        leds[i] = color;
      }
    }
    FastLED.show(); 
    
    String colorName = colorStr;
    colorName.toUpperCase();
    server.send(200, "text/plain", "Strip " + String(stripIndex) + " (" + String(stripLEDs) + " LEDs: " + String(startLED) + "-" + String(startLED + stripLEDs - 1) + ") set to " + colorName);
  });
  
  // LED configuration info endpoint
  server.on("/api/led/config", HTTP_GET, []() {
    String json = "{";
    json += "\"totalLEDs\":" + String(ledConfig.totalLEDs) + ",";
    json += "\"numSegments\":" + String(ledConfig.numSegments) + ",";
    json += "\"isConfigured\":" + String(ledConfig.isConfigured ? "true" : "false") + ",";
    json += "\"lastConfigSource\":\"" + ledConfig.lastConfigSource + "\",";
    json += "\"strips\":[";
    
    for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
      if (i > 0) json += ",";
      json += "{";
      json += "\"index\":" + String(i) + ",";
      json += "\"ledCount\":" + String(ledConfig.stripLEDs[i]) + ",";
      json += "\"startIndex\":" + String(ledConfig.stripStartIndices[i]) + ",";
      json += "\"endIndex\":" + String(ledConfig.stripStartIndices[i] + ledConfig.stripLEDs[i] - 1);
      json += "}";
    }
    
    json += "]}";
    server.send(200, "application/json", json);
  });
  
  // Simple version check endpoint
  server.on("/version", HTTP_GET, []() {
    server.send(200, "text/plain", "ESP32 Live Controller v2.0 - Dynamic Web Interface Active");
  });
  
  // Debug endpoint to check current configuration
  server.on("/debug", HTTP_GET, []() {
    String debug = "ESP32 Live Controller Debug Info:\n";
    debug += "================================\n";
    debug += "Total LEDs: " + String(ledConfig.totalLEDs) + "\n";
    debug += "Num Segments: " + String(ledConfig.numSegments) + "\n";
    debug += "Is Configured: " + String(ledConfig.isConfigured ? "Yes" : "No") + "\n";
    debug += "Last Config Source: " + ledConfig.lastConfigSource + "\n\n";
    debug += "Strip Details:\n";
    for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
      debug += "Strip " + String(i) + ": " + String(ledConfig.stripLEDs[i]) + " LEDs (";
      debug += String(ledConfig.stripStartIndices[i]) + "-" + String(ledConfig.stripStartIndices[i] + ledConfig.stripLEDs[i] - 1) + ")\n";
    }
    server.send(200, "text/plain", debug);
  });
  
  // Configuration management endpoints
  server.on("/config/clear", HTTP_GET, []() {
    clearSavedConfiguration();
    server.send(200, "text/plain", "Saved LED configuration cleared - device will use defaults until Unity sends new config");
  });
  
  server.on("/config/save", HTTP_GET, []() {
    if (ledConfig.isConfigured) {
      saveLEDConfiguration();
      server.send(200, "text/plain", "Current LED configuration saved to flash memory");
    } else {
      server.send(400, "text/plain", "No configuration to save - connect Unity first");
    }
  });
  
  // Main web interface - simplified and reliable
  server.on("/", HTTP_GET, []() {
    String html = "<!DOCTYPE html><html><head><title>ESP32 Live Controller v2.0</title>";
    html += "<meta name='viewport' content='width=device-width, initial-scale=1'>";
    html += "<meta http-equiv='Cache-Control' content='no-cache, no-store, must-revalidate'>";
    html += "<meta http-equiv='Pragma' content='no-cache'>";
    html += "<meta http-equiv='Expires' content='0'>";
    html += "<style>body{font-family:Arial;margin:20px;background:#f0f0f0}";
    html += ".container{max-width:800px;margin:0 auto;background:white;padding:20px;border-radius:10px}";
    html += ".btn{background:#007bff;color:white;border:none;padding:8px 12px;border-radius:4px;cursor:pointer;margin:2px;font-size:12px}";
    html += ".btn:hover{background:#0056b3}</style></head><body>";
    
    html += "<div class='container'><h1>🎮 ESP32 Live Controller v2.0</h1>";
    
    // Status section
    html += "<div style='background:#e8f5e8;padding:15px;border-radius:5px;margin-bottom:20px'>";
    html += "<h3>📊 System Status</h3>";
    html += "<p><strong>Total LEDs:</strong> " + String(ledConfig.totalLEDs) + "</p>";
    html += "<p><strong>Strips:</strong> " + String(ledConfig.numSegments) + "</p>";
    html += "<p><strong>Configured:</strong> " + String(ledConfig.isConfigured ? "Yes" : "No") + "</p>";
    html += "<p><strong>Source:</strong> " + ledConfig.lastConfigSource + "</p>";
    html += "</div>";
    
    // Global controls
    html += "<div><h3>🔧 Global Controls</h3>";
    html += "<button onclick=\"fetch('/api/led/color_test').then(r=>r.text()).then(data=>alert(data))\" class='btn'>🌈 Test All</button>";
    html += "<button onclick=\"fetch('/leds/off')\" class='btn'>⚫ Clear</button>";
    html += "<button onclick=\"location.reload()\" class='btn'>🔄 Refresh</button>";
    html += "</div>";
    
    // Strip controls (if configured)
    if (ledConfig.isConfigured && ledConfig.numSegments > 0) {
      html += "<div><h3>🎯 Strip Testing (Unity Config)</h3>";
      for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
        html += "<div style='border:1px solid #ddd;margin:10px 0;padding:10px;border-radius:5px'>";
        html += "<h4>Strip " + String(i) + " - " + String(ledConfig.stripLEDs[i]) + " LEDs (";
        html += String(ledConfig.stripStartIndices[i]) + "-" + String(ledConfig.stripStartIndices[i] + ledConfig.stripLEDs[i] - 1) + ")</h4>";
        
        // Color buttons for each strip
        String colors[] = {"red", "green", "blue", "yellow", "cyan", "magenta", "white"};
        String colorEmojis[] = {"🔴", "🟢", "🔵", "🟡", "🔵", "🟣", "⚪"};
        
        for (int c = 0; c < 7; c++) {
          html += "<button onclick=\"fetch('/api/led/single?strip=" + String(i) + "&color=" + colors[c] + "')\" class='btn'>";
          html += colorEmojis[c] + " " + colors[c] + "</button>";
        }
        html += "</div>";
      }
      html += "</div>";
    } else {
      html += "<div><h3>⚠️ No Unity Configuration</h3>";
      html += "<p>Connect Unity and run an animation to send LED configuration.</p></div>";
    }
    
    html += "</div></body></html>";
    server.send(200, "text/html", html);
  });
  
  server.begin();
  webSerial("✅ Web server started\n");
}

void setupOTA() {
  webSerial("🔄 Configuring OTA updates...\n");
  
  ArduinoOTA.setHostname("ESP32_Live_Controller");
  ArduinoOTA.setPassword("ledcontroller"); // Change this!
  
  ArduinoOTA.onStart([]() {
    String type = (ArduinoOTA.getCommand() == U_FLASH) ? "sketch" : "filesystem";
    webSerial("🔄 OTA Update starting: " + type + "\n");
    
    // Clear LEDs and show update progress
    FastLED.clear();
    FastLED.show();
  });
  
  ArduinoOTA.onEnd([]() {
    webSerial("\n✅ OTA Update complete\n");
    
    // Show completion pattern
    fill_solid(leds, totalActiveLEDs, CRGB::Green);
    FastLED.show();
  });
  
  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    // Show progress as blue bar
    int ledProgress = map(progress, 0, total, 0, totalActiveLEDs);
    
    FastLED.clear();
    for (int i = 0; i < ledProgress; i++) {
      leds[i] = CRGB::Blue;
    }
    FastLED.show();
    
    // Log progress every 10%
    if (progress % (total / 10) == 0) {
      webSerial("📊 OTA Progress: " + String(progress * 100 / total) + "%\n");
    }
  });
  
  ArduinoOTA.onError([](ota_error_t error) {
    webSerial("❌ OTA Error[" + String(error) + "]: ");
    switch (error) {
      case OTA_AUTH_ERROR: webSerial("Auth Failed\n"); break;
      case OTA_BEGIN_ERROR: webSerial("Begin Failed\n"); break;
      case OTA_CONNECT_ERROR: webSerial("Connect Failed\n"); break;
      case OTA_RECEIVE_ERROR: webSerial("Receive Failed\n"); break;
      case OTA_END_ERROR: webSerial("End Failed\n"); break;
      default: webSerial("Unknown Error\n"); break;
    }
    
    // Show error pattern
    for (int i = 0; i < 3; i++) {
      fill_solid(leds, totalActiveLEDs, CRGB::Red);
      FastLED.show();
      delay(500);
      FastLED.clear();
      FastLED.show();
      delay(500);
    }
  });
  
  ArduinoOTA.begin();
  webSerial("✅ OTA ready\n");
}

void showStartupPattern() {
  webSerial("🎆 Showing startup pattern...\n");
  
  // Test each segment individually
  for (int segment = 0; segment < ledConfig.numSegments; segment++) {
    // Light up segment in red, then green, then blue
    CRGB colors[] = {CRGB::Red, CRGB::Green, CRGB::Blue};
    
    for (int colorIndex = 0; colorIndex < 3; colorIndex++) {
      for (int led = segments[segment].startLED; led <= segments[segment].endLED; led++) {
        leds[led] = colors[colorIndex];
      }
      FastLED.show();
      delay(300);
      
      // Clear segment
      for (int led = segments[segment].startLED; led <= segments[segment].endLED; led++) {
        leds[led] = CRGB::Black;
      }
    }
  }
  
  // Final rainbow sweep
  rainbowPattern();
  FastLED.show();
  delay(1000);
  
  // Clear all
  FastLED.clear();
  FastLED.show();
  
  webSerial("✅ Startup pattern complete\n");
}

// =================== UDP PACKET HANDLING ===================
void handleUDPPackets() {
  int packetSize = udp.parsePacket();
  
  if (packetSize > 0) {
    // Read packet
    uint8_t buffer[1024]; // Smaller buffer for memory optimization
    int len = udp.read(buffer, sizeof(buffer));
    
    // Update statistics
    packetsReceived++;
    bytesReceived += len;
    lastPacketTime = millis();
    
    // Store Unity IP for monitoring
    if (!unityConnected) {
      unityIP = udp.remoteIP();
      unityConnected = true;
      webSerial("✅ Unity connected from " + unityIP.toString() + "\n");
    }
    
    // Try to parse as new packet format first
    if (len >= sizeof(PacketHeader)) {
      PacketHeader* header = (PacketHeader*)buffer;
      if (header->packetType <= 3) { // Valid packet types
        processNewPacket(buffer, len);
        return;
      }
    }
    
    // Fall back to legacy protocol
    if (len >= 4 && buffer[0] == PACKET_START_MARKER) {
      processLegacyPacket(buffer, len);
    } else {
      webSerial("⚠️ Invalid packet format (len=" + String(len) + ")\n");
    }
  }
}

void processNewPacket(uint8_t* buffer, int length) {
  PacketHeader* header = (PacketHeader*)buffer;
  
  switch (header->packetType) {
    case Connection:
      handleConnectionPacket(header);
      break;
      
    case LEDData:
      handleLEDDataPacket(buffer, length);
      break;
      
    case Configuration:
      handleConfigurationPacket(header);
      break;
      
    case Heartbeat:
      // Just acknowledge heartbeat
      break;
      
    default:
      webSerial("❓ Unknown packet type: " + String(header->packetType) + "\n");
      break;
  }
}

void processLegacyPacket(uint8_t* buffer, int len) {
  uint8_t command = buffer[1];
  uint8_t stripId = buffer[2];
  uint8_t data = buffer[3];
  
  // Only log non-LED-data commands to reduce spam
  if (command != COMMAND_LED_DATA) {
    webSerial("📨 Legacy Command: 0x" + String(command, HEX) + ", Strip: " + String(stripId) + ", Data: " + String(data) + "\n");
  }
  
  switch (command) {
    case COMMAND_CONNECTION:
      handleLegacyConnection();
      break;
      
    case COMMAND_TEST_PATTERN:
      handleTestPattern(data);
      break;
      
    case COMMAND_LED_DATA:
      handleLegacyLEDData(buffer, len);
      break;
      
    case COMMAND_LED_CONFIG:
      handleLEDConfigUpdate(buffer, len);
      break;
      
    default:
      webSerial("❓ Unknown legacy command: 0x" + String(command, HEX) + "\n");
      break;
  }
}

// ================== PACKET HANDLERS ==================

void handleConnectionPacket(PacketHeader* header) {
  webSerial("🤝 Connection packet received\n");
  // Could send acknowledgment back
}

void handleLEDDataPacket(uint8_t* buffer, int length) {
  PacketHeader* header = (PacketHeader*)buffer;
  
  // Parse LED data after header
  uint8_t* ledData = buffer + sizeof(PacketHeader);
  int dataLength = length - sizeof(PacketHeader);
  
  // Apply LED data based on format
  switch (header->dataFormat) {
    case RGB24:
      applyRGB24Data(ledData, dataLength);
      break;
    default:
      webSerial("❓ Unsupported LED data format: " + String(header->dataFormat) + "\n");
      break;
  }
}

void handleConfigurationPacket(PacketHeader* header) {
  webSerial("⚙️ Configuration packet received\n");
  // Could update settings based on packet data
}

void handleLEDConfigUpdate(uint8_t* buffer, int len) {
  webSerial("🔧 LED Configuration packet received\n");
  
  if (len < 12) { // Minimum: header(4) + totalLEDs(4) + numStrips(4) = 12 bytes
    webSerial("❌ LED config packet too short\n");
    return;
  }
  
  int pos = 4; // Skip legacy command header
  int newTotalLEDs;
  int newNumStrips;
  
  memcpy(&newTotalLEDs, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  memcpy(&newNumStrips, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  
  // Validate configuration
  if (newTotalLEDs <= 0 || newTotalLEDs > MAX_LEDS) {
    webSerial("❌ Invalid total LEDs: " + String(newTotalLEDs) + " (max: " + String(MAX_LEDS) + ")\n");
    return;
  }
  
  if (newNumStrips <= 0 || newNumStrips > 10) {
    webSerial("❌ Invalid number of strips: " + String(newNumStrips) + "\n");
    return;
  }
  
  // Check if we have enough data for all strip LED counts
  int expectedSize = 12 + (newNumStrips * 4);
  if (len < expectedSize) {
    webSerial("❌ Packet too short for " + String(newNumStrips) + " strips\n");
    return;
  }
  
  // Read and store individual strip LED counts
  webSerial("📋 Unity LED Configuration Received:\n");
  webSerial("   🔹 Total LEDs: " + String(newTotalLEDs) + "\n");
  webSerial("   🔹 Strips: " + String(newNumStrips) + "\n");
  
  int currentStartIndex = 0;
  for (int i = 0; i < newNumStrips; i++) {
    int stripLEDs;
    memcpy(&stripLEDs, &buffer[pos], sizeof(int));
    pos += sizeof(int);
    
    // Store strip information
    ledConfig.stripLEDs[i] = stripLEDs;
    ledConfig.stripStartIndices[i] = currentStartIndex;
    
    webSerial("   Strip " + String(i) + ": " + String(stripLEDs) + " LEDs (indices " + String(currentStartIndex) + "-" + String(currentStartIndex + stripLEDs - 1) + ")\n");
    
    currentStartIndex += stripLEDs;
  }
  
  // Clear unused strip entries
  for (int i = newNumStrips; i < 10; i++) {
    ledConfig.stripLEDs[i] = 0;
    ledConfig.stripStartIndices[i] = 0;
  }
  
  // Update LED configuration
  ledConfig.totalLEDs = newTotalLEDs;
  ledConfig.numSegments = newNumStrips;
  ledConfig.isConfigured = true;
  ledConfig.lastConfigSource = "Unity_DetailedMapping";
  
  // Update totalActiveLEDs for compatibility
  totalActiveLEDs = newTotalLEDs;
  
  webSerial("✅ Configuration saved successfully!\n");
  
  // Save configuration to flash memory
  saveLEDConfiguration();
  
  // Show confirmation pattern
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Green);
  FastLED.show();
  delay(300);
  FastLED.clear();
  FastLED.show();
}

void handleLegacyConnection() {
  webSerial("🤝 Legacy connection established\n");
}

void handleTestPattern(uint8_t pattern) {
  webSerial("🧪 Test pattern: " + String(pattern) + "\n");
  
  switch (pattern) {
    case 0: // Off
      FastLED.clear();
      break;
    case 1: // White
      fill_solid(leds, totalActiveLEDs, CRGB::White);
      break;
    case 2: // Rainbow
      rainbowPattern();
      break;
    case 3: // Red
      fill_solid(leds, totalActiveLEDs, CRGB::Red);
      break;
    case 4: // Green
      fill_solid(leds, totalActiveLEDs, CRGB::Green);
      break;
    case 5: // Blue
      fill_solid(leds, totalActiveLEDs, CRGB::Blue);
      break;
    default:
      return; // Don't update LEDs for unknown patterns
  }
  
  FastLED.show();
}

void handleLegacyLEDData(uint8_t* buffer, int len) {
  // Parse LED data packet - NEW: Support both individual strips and continuous strip
  // Individual Strip: [0xAA][0x01][stripId][ledCount][R][G][B]...[R][G][B]
  // Continuous Strip: [0xAA][0x01][0][0][ledCount_low][ledCount_high][R][G][B]...[R][G][B]
  
  if (len < 4) {
    webSerial("❌ Packet too short\n");
    return;
  }
  
  uint8_t stripId = buffer[2];
  uint8_t ledCountByte = buffer[3];
  
  // NEW: Detect continuous strip format (stripId=0, ledCountByte=0)
  if (stripId == 0 && ledCountByte == 0 && len >= 6) {
    // Continuous strip format with 16-bit LED count
    uint16_t totalLEDs = buffer[4] | (buffer[5] << 8);
    
    webSerial("🌈 Continuous Strip Data: " + String(totalLEDs) + " LEDs (" + String(len) + " bytes)\n");
    
    int expectedSize = 6 + (totalLEDs * 3);
    if (len != expectedSize) {
      webSerial("❌ Invalid continuous strip size: expected " + String(expectedSize) + ", got " + String(len) + "\n");
      return;
    }
    
    // Apply colors to entire continuous strip
    for (int i = 0; i < totalLEDs && i < MAX_LEDS; i++) {
      int colorOffset = 6 + (i * 3);
      leds[i] = CRGB(buffer[colorOffset], buffer[colorOffset + 1], buffer[colorOffset + 2]);
    }
    
    // Update the entire strip
    FastLED.show();
    
    // DEBUG: Show first few LED colors for mapping verification
    if (totalLEDs > 0) {
      webSerial("🎨 First 5 LEDs: ");
      for (int i = 0; i < 5 && i < totalLEDs; i++) {
        webSerial("(" + String(leds[i].r) + "," + String(leds[i].g) + "," + String(leds[i].b) + ") ");
      }
      webSerial("\n");
    }
    
    webSerial("✅ Updated continuous strip with " + String(totalLEDs) + " LEDs\n");
    return;
  }
  
  // LEGACY: Individual strip format
  uint8_t ledCount = ledCountByte;
  
  // Validate strip ID and LED count using dynamic config
  if (stripId >= ledConfig.numSegments) {
    webSerial("❌ Invalid strip ID: " + String(stripId) + " (max: " + String(ledConfig.numSegments - 1) + ")\n");
    return;
  }
  
  if (ledCount > ledConfig.ledsPerSegment) {
    webSerial("❌ Too many LEDs for strip " + String(stripId) + ": " + String(ledCount) + " (max: " + String(ledConfig.ledsPerSegment) + ")\n");
    return;
  }
  
  // Expected packet size: header(4) + (ledCount * 3)
  int expectedSize = 4 + (ledCount * 3);
  if (len != expectedSize) {
    webSerial("❌ Invalid legacy LED data size: expected " + String(expectedSize) + ", got " + String(len) + "\n");
    return;
  }
  
  // Apply colors to the specified segment
  int ledOffset = segments[stripId].startLED;
  for (int i = 0; i < ledCount && i < segments[stripId].activeCount; i++) {
    int ledIndex = ledOffset + i;
    if (ledIndex < MAX_LEDS) {
      int colorOffset = 4 + (i * 3);
      leds[ledIndex] = CRGB(buffer[colorOffset], buffer[colorOffset + 1], buffer[colorOffset + 2]);
    }
  }
  
  FastLED.show();
}

void applyRGB24Data(uint8_t* data, int length) {
  // Apply RGB24 data directly to LED array
  int ledCount = length / 3;
  
  for (int i = 0; i < ledCount && i < totalActiveLEDs; i++) {
    int dataOffset = i * 3;
    leds[i] = CRGB(data[dataOffset], data[dataOffset + 1], data[dataOffset + 2]);
  }
  
  FastLED.show();
}

// ================== UTILITY FUNCTIONS ==================

void rainbowPattern() {
  for (int i = 0; i < totalActiveLEDs; i++) {
    leds[i] = CHSV(map(i, 0, totalActiveLEDs, 0, 255), 255, 255);
  }
}

void testAllSegments() {
  webSerial("🧪 Testing all segments...\n");
  
  CRGB colors[] = {CRGB::Red, CRGB::Green, CRGB::Blue};
  
  for (int colorIndex = 0; colorIndex < 3; colorIndex++) {
    for (int segment = 0; segment < ledConfig.numSegments; segment++) {
      // Light up segment
      for (int led = segments[segment].startLED; led <= segments[segment].endLED; led++) {
        leds[led] = colors[colorIndex];
      }
    }
    FastLED.show();
    delay(500);
    FastLED.clear();
    FastLED.show();
    delay(200);
  }
  
  webSerial("✅ Segment test complete\n");
}

void updateStatistics() {
  static unsigned long lastPacketCount = 0;
  
  if (millis() - lastStatsTime > 10000) { // Every 10 seconds
    // Calculate FPS
    unsigned long packetDiff = packetsReceived - lastPacketCount;
    fps = packetDiff / 10.0;
    
    // Print statistics (reduced logging for memory optimization)
    webSerial("📊 FPS: " + String(fps, 1) + " | Packets: " + String(packetsReceived) + 
             " | Unity: " + String(unityConnected ? "✅" : "❌") + "\n");
    
    lastStatsTime = millis();
    lastPacketCount = packetsReceived;
  }
}

void heartbeat() {
  // Quick flash of first LED to show ESP32 is alive
  if (totalActiveLEDs > 0) {
    CRGB originalColor = leds[0];
    leds[0] = CRGB::Green;
    FastLED.show();
    delay(50);
    leds[0] = originalColor;
    FastLED.show();
  }
}

void handleSerialCommands() {
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    command.toLowerCase();
    
    if (command == "status") {
      printSystemStatus();
    } else if (command == "rainbow") {
      rainbowPattern();
      FastLED.show();
      Serial.println("Rainbow pattern applied");
    } else if (command == "white") {
      fill_solid(leds, totalActiveLEDs, CRGB::White);
      FastLED.show();
      Serial.println("All LEDs set to white");
    } else if (command == "off") {
      FastLED.clear();
      FastLED.show();
      Serial.println("All LEDs turned off");
    } else if (command == "test") {
      testAllSegments();
    } else if (command == "restart") {
      Serial.println("Restarting ESP32...");
      ESP.restart();
    } else {
      Serial.println("Commands: status, rainbow, white, off, test, restart");
    }
  }
}

void printSystemStatus() {
  Serial.println("\n📊 ESP32 LIVE CONTROLLER STATUS");
  Serial.println("===============================");
  Serial.println("💡 LED Controller: " + String(totalActiveLEDs) + " LEDs on GPIO " + String(LED_PIN));
  Serial.println("📶 WiFi: " + WiFi.localIP().toString() + " (" + String(WiFi.RSSI()) + " dBm)");
  Serial.println("🎮 Unity: " + String(unityConnected ? "Connected" : "Disconnected"));
  Serial.println("📡 UDP Port: " + String(UDP_PORT));
  Serial.println("🌐 Web: http://" + WiFi.localIP().toString());
  Serial.println("📊 Packets: " + String(packetsReceived) + " (" + String(fps, 1) + " FPS)");
  Serial.println("💾 Free Heap: " + String(ESP.getFreeHeap()) + " bytes");
  Serial.println("⏰ Uptime: " + String(millis() / 1000) + " seconds");
  Serial.println("===============================");
}

String generateDynamicStatusPage() {
  // Generate timestamp for cache busting
  String timestamp = String(millis());
  
  String html = "<!DOCTYPE html><html><head>";
  html += "<title>🎮 ESP32 Live Controller v2.0</title>";
  html += "<meta charset='UTF-8'>";
  html += "<meta name='viewport' content='width=device-width, initial-scale=1.0'>";
  html += "<meta http-equiv='Cache-Control' content='no-cache, no-store, must-revalidate'>";
  html += "<meta http-equiv='Pragma' content='no-cache'>";
  html += "<meta http-equiv='Expires' content='0'>";
  html += "<style>";
  html += "body { font-family: Arial, sans-serif; margin: 10px; background: #f0f0f0; }";
  html += ".container { max-width: 1000px; margin: 0 auto; background: white; padding: 15px; border-radius: 8px; }";
  html += "h1 { color: #333; text-align: center; margin: 10px 0; }";
  html += ".status { display: flex; justify-content: space-around; margin: 15px 0; flex-wrap: wrap; }";
  html += ".status-item { text-align: center; padding: 8px; background: #f8f9fa; border-radius: 5px; margin: 2px; min-width: 100px; }";
  html += ".connected { color: #28a745; }";
  html += ".disconnected { color: #dc3545; }";
  html += ".controls { margin: 15px 0; }";
  html += ".btn { display: inline-block; margin: 3px; padding: 8px 12px; background: #007bff; color: white; text-decoration: none; border-radius: 4px; font-size: 12px; }";
  html += ".btn:hover { background: #0056b3; }";
  html += ".btn-red { background: #dc3545; }";
  html += ".btn-green { background: #28a745; }";
  html += ".btn-blue { background: #007bff; }";
  html += ".strip-section { margin: 10px 0; padding: 10px; background: #f8f9fa; border-radius: 5px; }";
  html += ".strip-title { font-weight: bold; margin-bottom: 5px; }";
  html += "</style>";
  html += "</head><body>";
  
  html += "<div class='container'>";
  html += "<h1>🎮 ESP32 Live Controller v2.0</h1>";
  
  // Status section
  html += "<div class='status'>";
  html += "<div class='status-item'><strong>Unity</strong><br>";
  html += "<span class='" + String(unityConnected ? "connected'>✅ Connected" : "disconnected'>❌ Disconnected") + "</span></div>";
  html += "<div class='status-item'><strong>Total LEDs</strong><br><span>" + String(ledConfig.totalLEDs) + "</span></div>";
  html += "<div class='status-item'><strong>Strips</strong><br><span>" + String(ledConfig.numSegments) + "</span></div>";
  html += "<div class='status-item'><strong>Configured</strong><br><span>" + String(ledConfig.isConfigured ? "✅ Yes" : "❌ No") + "</span></div>";
  html += "<div class='status-item'><strong>FPS</strong><br><span>" + String(fps, 1) + "</span></div>";
  html += "</div>";
  
  // Global controls
  html += "<div class='controls'>";
  html += "<h3>🌐 Global Controls</h3>";
  html += "<a href='javascript:testAllLEDs()' class='btn'>🌈 Test All</a>";
  html += "<a href='javascript:clearAllLEDs()' class='btn'>⚫ Clear</a>";
  html += "<a href='/test/rainbow' class='btn'>🌈 Rainbow</a>";
  html += "<a href='/debug' class='btn'>🔍 Debug</a>";
  html += "</div>";
  
  // Brightness controls
  html += "<div class='controls'>";
  html += "<h3>💡 Brightness Controls (Current: " + String(currentBrightness) + "/255)</h3>";
  html += "<a href='/brightness/max' class='btn'>🔆 MAX</a>";
  html += "<a href='/brightness/high' class='btn'>🔅 HIGH</a>";
  html += "<a href='/brightness/medium' class='btn'>🌗 MEDIUM</a>";
  html += "<a href='/brightness/low' class='btn'>🌑 LOW</a>";
  html += "</div>";
  
  // Individual strip controls (only if configured)
  if (ledConfig.isConfigured && ledConfig.numSegments > 0) {
    html += "<div class='controls'>";
    html += "<h3>🎯 Individual Strip Controls</h3>";
    
    for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
      html += "<div class='strip-section'>";
      html += "<div class='strip-title'>Strip " + String(i) + " (" + String(ledConfig.stripLEDs[i]) + " LEDs: " + String(ledConfig.stripStartIndices[i]) + "-" + String(ledConfig.stripStartIndices[i] + ledConfig.stripLEDs[i] - 1) + ")</div>";
      html += "<a href='javascript:setStripColor(" + String(i) + ",\"red\")' class='btn btn-red'>🔴 Red</a>";
      html += "<a href='javascript:setStripColor(" + String(i) + ",\"green\")' class='btn btn-green'>🟢 Green</a>";
      html += "<a href='javascript:setStripColor(" + String(i) + ",\"blue\")' class='btn btn-blue'>🔵 Blue</a>";
      html += "<a href='javascript:setStripColor(" + String(i) + ",\"white\")' class='btn'>⚪ White</a>";
      html += "</div>";
    }
    html += "</div>";
  } else {
    html += "<div class='controls'>";
    html += "<h3>⚠️ Unity Configuration Required</h3>";
    html += "<p>Connect Unity and send LED configuration to enable individual strip controls.</p>";
    html += "</div>";
  }
  
  // JavaScript functions
  html += "<script>";
  html += "function testAllLEDs() {";
  html += "  fetch('/api/led/color_test').then(r => r.text()).then(data => console.log(data));";
  html += "}";
  html += "function clearAllLEDs() {";
  html += "  fetch('/leds/off').then(r => r.text()).then(data => console.log(data));";
  html += "}";
  html += "function setStripColor(strip, color) {";
  html += "  fetch('/api/led/single?strip=' + strip + '&color=' + color).then(r => r.text()).then(data => console.log(data));";
  html += "}";
  html += "</script>";
  
  html += "</div></body></html>";
  
  return html;
}

// =================== CONFIGURATION PERSISTENCE ===================

void loadLEDConfiguration() {
  webSerial("💾 Loading saved LED configuration...\n");
  
  preferences.begin("ledconfig", false); // Read-only mode
  
  // Check if configuration exists
  if (preferences.isKey("configured")) {
    ledConfig.totalLEDs = preferences.getInt("totalLEDs", 100);
    ledConfig.numSegments = preferences.getInt("numSegments", 1);
    ledConfig.isConfigured = preferences.getBool("configured", false);
    ledConfig.lastConfigSource = preferences.getString("source", "default");
    
    // Load individual strip data
    for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
      String stripKey = "strip" + String(i);
      String indexKey = "index" + String(i);
      ledConfig.stripLEDs[i] = preferences.getInt(stripKey.c_str(), 0);
      ledConfig.stripStartIndices[i] = preferences.getInt(indexKey.c_str(), 0);
    }
    
    webSerial("✅ Loaded saved configuration:\n");
    webSerial("   🔹 Total LEDs: " + String(ledConfig.totalLEDs) + "\n");
    webSerial("   🔹 Strips: " + String(ledConfig.numSegments) + "\n");
    webSerial("   🔹 Source: " + ledConfig.lastConfigSource + "\n");
    
    for (int i = 0; i < ledConfig.numSegments && i < 3; i++) { // Show first 3 strips
      webSerial("   Strip " + String(i) + ": " + String(ledConfig.stripLEDs[i]) + " LEDs\n");
    }
    if (ledConfig.numSegments > 3) {
      webSerial("   ... and " + String(ledConfig.numSegments - 3) + " more strips\n");
    }
  } else {
    webSerial("📝 No saved configuration found - using defaults\n");
    webSerial("💡 Unity will send configuration on first connection\n");
  }
  
  preferences.end();
}

void saveLEDConfiguration() {
  webSerial("💾 Saving LED configuration to flash memory...\n");
  
  preferences.begin("ledconfig", false); // Read-write mode
  
  preferences.putInt("totalLEDs", ledConfig.totalLEDs);
  preferences.putInt("numSegments", ledConfig.numSegments);
  preferences.putBool("configured", ledConfig.isConfigured);
  preferences.putString("source", ledConfig.lastConfigSource);
  
  // Save individual strip data
  for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
    String stripKey = "strip" + String(i);
    String indexKey = "index" + String(i);
    preferences.putInt(stripKey.c_str(), ledConfig.stripLEDs[i]);
    preferences.putInt(indexKey.c_str(), ledConfig.stripStartIndices[i]);
  }
  
  // Clear unused strip entries in storage
  for (int i = ledConfig.numSegments; i < 10; i++) {
    String stripKey = "strip" + String(i);
    String indexKey = "index" + String(i);
    preferences.remove(stripKey.c_str());
    preferences.remove(indexKey.c_str());
  }
  
  preferences.end();
  
  webSerial("✅ LED configuration saved to flash memory\n");
  webSerial("🔄 Configuration will persist through power cycles\n");
}

void clearSavedConfiguration() {
  webSerial("🗑️ Clearing saved LED configuration...\n");
  
  preferences.begin("ledconfig", false);
  preferences.clear();
  preferences.end();
  
  // Reset to defaults
  ledConfig.totalLEDs = 100;
  ledConfig.numSegments = 1;
  ledConfig.isConfigured = false;
  ledConfig.lastConfigSource = "default";
  
  webSerial("✅ Saved configuration cleared - using defaults\n");
}

void webSerial(String message) {
  // Print to actual serial
  Serial.print(message);
  
  // Add timestamp for web buffer
  String timestamped = "[" + String(millis() / 1000) + "s] " + message;
  serialBuffer += timestamped;
  
  // Trim buffer if too long (reduced for memory optimization)
  if (serialBuffer.length() > MAX_SERIAL_BUFFER) {
    serialBuffer = serialBuffer.substring(serialBuffer.length() - MAX_SERIAL_BUFFER + 200);
  }
}
