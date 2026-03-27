/*
 * ESP32 Animation Player for Unity Branching LED Animator
 * Optimized for standalone animation playback and playlist management
 * 
 * Features:
 * - SPIFFS animation storage and playback
 * - Playlist management with scheduling
 * - Web-based animation library browser
 * - WiFi OTA updates for animation uploads
 * - UDP animation upload from Unity
 * - Standalone operation (no live Unity required)
 * 
 * MEMORY OPTIMIZED: No live Unity streaming functionality
 * Use ESP32_Live_Controller.ino for live Unity connectivity
 * 
 * Hardware Setup:
 * - ESP32 development board
 * - Single WS2812B LED strip (continuous, ~99-150 LEDs total)
 * - GPIO 19 → LED strip data input (through level shifter recommended)
 * - 5V power supply (adequate for your LED count)
 * - GND → Common ground (ESP32, PSU, LED strip)
 */

#include <WiFi.h>
#include <WiFiUdp.h>
#include <WebServer.h>
#include <ArduinoOTA.h>
#include <FastLED.h>
#include <SPIFFS.h>
#include <ArduinoJson.h>
#include <Preferences.h>

// ===================== CONFIGURATION =====================
// WiFi Configuration
const char* ssid = "Pneuhaus";           // Your WiFi network name
const char* password = "Pneuhaus310";    // Your WiFi password

// LED Configuration - Default values (will be updated by Unity)
#define LED_PIN 19
#define MAX_LEDS 1000             // Maximum LEDs supported (ESP32 can handle much more on a single pin)
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
#define UDP_PORT 8888             // Animation upload port (matches Unity ESP32Communicator)
#define WEB_PORT 80               // Web server port

// Animation upload commands
#define PACKET_START_MARKER 0xAA
#define COMMAND_LED_CONFIG 0x05          // NEW: LED configuration packet
#define COMMAND_ANIM_UPLOAD 0x10
#define COMMAND_PLAYLIST_UPLOAD 0x11
#define COMMAND_LIBRARY_STATUS 0x12
#define COMMAND_ANIM_PLAY 0x13
#define COMMAND_ANIM_STOP 0x14

// ==================== GLOBAL VARIABLES ====================
// LED Array
CRGB leds[MAX_LEDS];

// Network components
WiFiUDP udp;
WebServer server(WEB_PORT);

// Configuration storage
Preferences preferences;

// Animation library
struct StoredAnimation {
  String name;
  float duration;
  int frameRate;
  int ledCount;
  int frameCount;
  String filename;
  size_t fileSize;
  bool isValid;
};

struct PlaylistItem {
  String animationName;
  float duration;
  float speedMultiplier;
  int priority;
  bool enabled;
};

// Animation storage
std::vector<StoredAnimation> animationLibrary;
std::vector<PlaylistItem> currentPlaylist;
String playlistName = "Default Playlist";
bool playlistLoopEnabled = true;
bool isPlayingStandalone = false;
int currentPlaylistIndex = 0;
unsigned long animationStartTime = 0;
unsigned long lastFrameTime = 0;

// Animation upload state
struct AnimationUpload {
  String name;
  int totalChunks;
  int receivedChunks;
  File uploadFile;
  bool inProgress;
  unsigned long lastChunkTime;
};
AnimationUpload currentUpload;

// LED state
uint8_t currentBrightness = 255;  // Full brightness by default
bool ledsEnabled = true;

// =================== FUNCTION DECLARATIONS ===================
void handleSerialCommands();
void printSystemStatus();
void listAnimations();
void printStorageInfo();
void playSpecificAnimation(String animName);
void testLEDStrip();
void printLEDInfo();
void testColorOrder();
String generateAnimationsJSON();
String generatePlaylistJSON();
String generateStorageJSON();
String generateLEDInfoJSON();

// ======================= SETUP =======================
void setup() {
  Serial.begin(115200);
  Serial.println("\n🎬 ESP32 Animation Player Starting...");
  Serial.println("Features: Animation Storage + Playlist + Standalone Playback + Persistent Config");
  
  // Load saved LED configuration
  loadLEDConfiguration();
  
  // Initialize LEDs
  setupLEDs();
  
  // Connect to WiFi
  setupWiFi();
  
  // Initialize SPIFFS for animation storage
  setupSPIFFS();
  
  // Load animation library
  loadAnimationLibrary();
  
  // Initialize UDP for animation uploads
  setupUDP();
  
  // Initialize Web Server
  setupWebServer();
  
  // Initialize OTA
  setupOTA();
  
  // Show startup pattern
  showStartupPattern();
  
  Serial.println("✅ ESP32 Animation Player Ready!");
  Serial.println("📡 UDP listening on port " + String(UDP_PORT) + " for animation uploads");
  Serial.println("🌐 Web interface at http://" + WiFi.localIP().toString());
  Serial.println("🔄 OTA enabled as 'ESP32_Animation_Player'");
  Serial.println("🎭 Use web interface to manage animations and playlists");
  
  // Show configuration status
  if (!ledConfig.isConfigured) {
    Serial.println("⚙️ Using default LED configuration (100 LEDs, 1 strip)");
    Serial.println("💡 For complex setups, connect Unity to send proper LED configuration");
  } else {
    Serial.println("✅ LED configuration: " + String(ledConfig.totalLEDs) + " LEDs, " + String(ledConfig.numSegments) + " strips (source: " + ledConfig.lastConfigSource + ")");
  }
  
  // Auto-start playlist if available
  if (!currentPlaylist.empty()) {
    delay(2000); // Give user time to see startup messages
    Serial.println("🎬 Auto-starting playlist with " + String(currentPlaylist.size()) + " items");
    startStandalonePlayback();
  } else {
    Serial.println("⚠️ No playlist available for auto-start");
    Serial.println("📚 Available animations: " + String(animationLibrary.size()));
    if (animationLibrary.size() > 0) {
      Serial.println("🔧 Creating default playlist from available animations...");
      createDefaultPlaylist();
      delay(1000);
      startStandalonePlayback();
    }
  }
}

// ======================= MAIN LOOP =======================
void loop() {
  // Handle OTA updates (highest priority)
  ArduinoOTA.handle();
  
  // Handle web server requests
  server.handleClient();
  
  // Handle UDP packets for animation uploads
  handleUDPPackets();
  
  // Update standalone animation playback
  updateStandalonePlayback();
  
  // Check for stalled uploads (timeout after 10 seconds)
  if (currentUpload.inProgress && (millis() - currentUpload.lastChunkTime > 10000)) {
    Serial.println("⏰ Upload timeout - completing with " + String(currentUpload.receivedChunks) + "/" + String(currentUpload.totalChunks) + " chunks");
    if (currentUpload.uploadFile) {
      currentUpload.uploadFile.close();
    }
    currentUpload.inProgress = false;
    loadAnimationLibrary();
  }
  
  // Handle serial commands
  handleSerialCommands();
  
  // Small delay to prevent watchdog issues
  delay(1);
}

// ===================== SETUP FUNCTIONS =====================
void setupLEDs() {
  Serial.println("💡 Initializing WS2812B LED strip...");
  
  // Initialize FastLED with maximum capacity (will use actual count from config)
  FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, MAX_LEDS);
  FastLED.setBrightness(currentBrightness);
  FastLED.setCorrection(TypicalLEDStrip);
  
  // Clear all LEDs
  FastLED.clear();
  FastLED.show();
  
  Serial.println("✅ FastLED initialized on GPIO " + String(LED_PIN) + " (capacity: " + String(MAX_LEDS) + ")");
  Serial.println("🔧 Using LED config: " + String(ledConfig.totalLEDs) + " total, " + String(ledConfig.numSegments) + " segments");
  Serial.println("📡 LED configuration will be updated when Unity sends config packet");
}

void setupWiFi() {
  Serial.println("📶 Connecting to WiFi: " + String(ssid));
  
  // Show WiFi connecting pattern
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Blue);
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
    Serial.println("\n✅ WiFi connected!");
    Serial.println("📍 IP address: " + WiFi.localIP().toString());
    
    // Show success pattern (green)
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Green);
    FastLED.show();
    delay(1000);
  } else {
    Serial.println("\n❌ WiFi connection failed! Starting Access Point mode...");
    setupAccessPoint();
  }
  
  // Clear LEDs and restore brightness
  FastLED.clear();
  FastLED.setBrightness(currentBrightness);
  FastLED.show();
}

void setupAccessPoint() {
  // Create unique AP name based on chip ID
  String apName = "ESP32_Animator_" + String((uint32_t)ESP.getEfuseMac(), HEX);
  const char* apPassword = "animation123"; // Change this to your preferred password
  
  Serial.println("🔶 Starting Access Point mode...");
  Serial.println("📡 AP Name: " + apName);
  Serial.println("🔑 AP Password: " + String(apPassword));
  
  // Set up Access Point
  WiFi.mode(WIFI_AP);
  WiFi.softAP(apName.c_str(), apPassword);
  
  IPAddress apIP = WiFi.softAPIP();
  Serial.println("✅ Access Point started!");
  Serial.println("📍 AP IP address: " + apIP.toString());
  Serial.println("🌐 Connect to '" + apName + "' and visit http://" + apIP.toString());
  
  // Show AP mode pattern (orange/yellow)
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Orange);
  FastLED.show();
  delay(1000);
}

void setupSPIFFS() {
  Serial.println("📁 Initializing SPIFFS for animation storage...");
  if (!SPIFFS.begin(true)) {
    Serial.println("❌ SPIFFS initialization failed!");
    return;
  }
  
  size_t totalBytes = SPIFFS.totalBytes();
  size_t usedBytes = SPIFFS.usedBytes();
  float usagePercent = (float)usedBytes / totalBytes * 100;
  
  Serial.println("✅ SPIFFS ready: " + String(usedBytes / 1024) + "KB / " + 
                String(totalBytes / 1024) + "KB (" + String(usagePercent, 1) + "%)");
  
  // SPIFFS uses flat file structure (no subdirectories needed)
  Serial.println("✅ SPIFFS ready for animation storage");
  
  // List all files for debugging
  Serial.println("📂 SPIFFS contents:");
  File root = SPIFFS.open("/");
  if (root) {
    File file = root.openNextFile();
    while (file) {
      if (file.isDirectory()) {
        Serial.println("  📁 " + String(file.name()) + "/");
      } else {
        Serial.println("  📄 " + String(file.name()) + " (" + String(file.size()) + " bytes)");
      }
      file = root.openNextFile();
    }
    root.close();
  } else {
    Serial.println("❌ Failed to open root directory");
  }
}

void setupUDP() {
  Serial.println("📡 Starting UDP server on port " + String(UDP_PORT) + "...");
  
  if (udp.begin(UDP_PORT)) {
    Serial.println("✅ UDP server started for animation uploads");
  } else {
    Serial.println("❌ UDP server failed to start");
  }
}

void setupWebServer() {
  Serial.println("🌐 Starting web server on port " + String(WEB_PORT) + "...");
  
  // Root page - animation library and controls
  server.on("/", HTTP_GET, []() {
    String html = generateMainPage();
    server.send(200, "text/html", html);
  });
  
  // Debug/Serial log endpoint
  server.on("/api/logs", HTTP_GET, []() {
    String logs = "ESP32 Debug Logs:\n";
    logs += "=================\n";
    logs += "Uptime: " + String(millis() / 1000) + "s\n";
    logs += "Free Heap: " + String(ESP.getFreeHeap()) + " bytes\n";
    logs += "Playing: " + String(isPlayingStandalone ? "Yes" : "No") + "\n";
    logs += "Animations: " + String(animationLibrary.size()) + "\n";
    logs += "Playlist Items: " + String(currentPlaylist.size()) + "\n";
    logs += "LED Config: " + String(ledConfig.totalLEDs) + " LEDs\n";
    logs += "WiFi RSSI: " + String(WiFi.RSSI()) + " dBm\n";
    server.send(200, "text/plain", logs);
  });
  
  // Animation library API
  server.on("/api/animations", HTTP_GET, []() {
    String json = generateAnimationsJSON();
    server.send(200, "application/json", json);
  });
  
  // Playlist API
  server.on("/api/playlist", HTTP_GET, []() {
    String json = generatePlaylistJSON();
    server.send(200, "application/json", json);
  });
  
  // Playback controls
  server.on("/api/play", HTTP_GET, []() {
    // Check if playlist exists, if not create default
    if (currentPlaylist.empty()) {
      Serial.println("📋 No playlist found - creating default from available animations");
      createDefaultPlaylist();
    }
    
    if (currentPlaylist.empty()) {
      server.send(400, "text/plain", "No animations available to play");
    } else {
      startStandalonePlayback();
      server.send(200, "text/plain", "Playback started");
    }
  });
  
  server.on("/api/stop", HTTP_GET, []() {
    stopStandalonePlayback();
    server.send(200, "text/plain", "Playback stopped");
  });
  
  server.on("/api/next", HTTP_GET, []() {
    if (isPlayingStandalone) {
      nextPlaylistItem();
      server.send(200, "text/plain", "Next animation");
    } else {
      server.send(400, "text/plain", "Not playing");
    }
  });
  
  // Storage info
  server.on("/api/storage", HTTP_GET, []() {
    String json = generateStorageJSON();
    server.send(200, "application/json", json);
  });
  
  // LED configuration info
  server.on("/api/ledinfo", HTTP_GET, []() {
    String json = generateLEDInfoJSON();
    server.send(200, "application/json", json);
  });
  
  // Delete animation
  server.on("/api/delete", HTTP_GET, []() {
    String animName = server.arg("name");
    if (animName.length() > 0) {
      deleteAnimation(animName);
      server.send(200, "text/plain", "Animation deleted");
    } else {
      server.send(400, "text/plain", "No animation name provided");
    }
  });
  
  // Toggle playlist item
  server.on("/api/playlist/toggle", HTTP_GET, []() {
    int index = server.arg("index").toInt();
    if (index >= 0 && index < currentPlaylist.size()) {
      currentPlaylist[index].enabled = !currentPlaylist[index].enabled;
      savePlaylist();
      server.send(200, "text/plain", "Item toggled");
    } else {
      server.send(400, "text/plain", "Invalid index");
    }
  });
  
  // Set playlist loop
  server.on("/api/playlist/loop", HTTP_GET, []() {
    String enable = server.arg("enable");
    playlistLoopEnabled = (enable == "true");
    savePlaylist();
    server.send(200, "text/plain", "Loop " + String(playlistLoopEnabled ? "enabled" : "disabled"));
  });
  
  // Add animation to playlist
  server.on("/api/playlist/add", HTTP_GET, []() {
    String animName = server.arg("name");
    if (animName.length() == 0) {
      server.send(400, "text/plain", "Animation name required");
      return;
    }
    
    // Check if animation exists in library
    bool found = false;
    StoredAnimation* anim = nullptr;
    for (auto& a : animationLibrary) {
      if (a.name == animName) {
        found = true;
        anim = &a;
        break;
      }
    }
    
    if (!found) {
      server.send(404, "text/plain", "Animation not found in library");
      return;
    }
    
    // Check if already in playlist
    for (const auto& item : currentPlaylist) {
      if (item.animationName == animName) {
        server.send(400, "text/plain", "Animation already in playlist");
        return;
      }
    }
    
    // Add to playlist
    PlaylistItem item;
    item.animationName = animName;
    item.duration = anim->duration;
    item.speedMultiplier = 1.0f;
    item.priority = 1;
    item.enabled = true;
    currentPlaylist.push_back(item);
    
    savePlaylist();
    server.send(200, "text/plain", "Animation added to playlist");
  });
  
  // Remove animation from playlist
  server.on("/api/playlist/remove", HTTP_GET, []() {
    int index = server.arg("index").toInt();
    if (index >= 0 && index < currentPlaylist.size()) {
      String animName = currentPlaylist[index].animationName;
      currentPlaylist.erase(currentPlaylist.begin() + index);
      savePlaylist();
      
      // Adjust current index if necessary
      if (isPlayingStandalone && currentPlaylistIndex >= index) {
        if (currentPlaylistIndex > 0) {
          currentPlaylistIndex--;
        }
      }
      
      server.send(200, "text/plain", "Removed " + animName + " from playlist");
    } else {
      server.send(400, "text/plain", "Invalid playlist index");
    }
  });
  
  // Clear entire playlist
  server.on("/api/playlist/clear", HTTP_GET, []() {
    currentPlaylist.clear();
    playlistName = "Empty Playlist";
    savePlaylist();
    
    if (isPlayingStandalone) {
      stopStandalonePlayback();
    }
    
    server.send(200, "text/plain", "Playlist cleared");
  });
  
  // Create playlist from all available animations
  server.on("/api/playlist/create_default", HTTP_GET, []() {
    createDefaultPlaylist();
    server.send(200, "text/plain", "Default playlist created with " + String(currentPlaylist.size()) + " animations");
  });
  
  // LED test endpoints
  server.on("/api/ledtest", HTTP_GET, []() {
    testLEDStrip();
    server.send(200, "text/plain", "LED test completed - check LEDs!");
  });
  
  // Individual LED debugging endpoints
  server.on("/api/led/all_white", HTTP_GET, []() {
    stopStandalonePlayback();
    fill_solid(leds, ledConfig.totalLEDs, CRGB::White);
    FastLED.show();
    server.send(200, "text/plain", "All LEDs set to WHITE (" + String(ledConfig.totalLEDs) + " LEDs)");
  });
  
  server.on("/api/led/all_black", HTTP_GET, []() {
    stopStandalonePlayback();
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
    FastLED.show();
    server.send(200, "text/plain", "All LEDs turned OFF");
  });
  
  server.on("/api/led/rainbow", HTTP_GET, []() {
    stopStandalonePlayback();
    for (int i = 0; i < ledConfig.totalLEDs; i++) {
      leds[i] = CHSV((i * 256 / ledConfig.totalLEDs), 255, 200);
    }
    FastLED.show();
    server.send(200, "text/plain", "Rainbow pattern displayed across " + String(ledConfig.totalLEDs) + " LEDs");
  });
  
  server.on("/api/led/color_test", HTTP_GET, []() {
    stopStandalonePlayback();
    
    if (ledConfig.isConfigured && ledConfig.numSegments > 1) {
      // Show actual strip/segment mapping with different colors
      CRGB colors[] = {CRGB::Red, CRGB::Green, CRGB::Blue, CRGB::Yellow, CRGB::Cyan, CRGB::Magenta, CRGB::Orange, CRGB::Purple, CRGB::Pink, CRGB::White};
      
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
      // Fallback to thirds if not configured with individual strips
      int third = ledConfig.totalLEDs / 3;
      for (int i = 0; i < third; i++) {
        leds[i] = CRGB::Red;
      }
      for (int i = third; i < third * 2; i++) {
        leds[i] = CRGB::Green;
      }
      for (int i = third * 2; i < ledConfig.totalLEDs; i++) {
        leds[i] = CRGB::Blue;
      }
      FastLED.show();
      server.send(200, "text/plain", "Color sections: RED | GREEN | BLUE (using " + String(ledConfig.totalLEDs) + " LEDs)");
    }
  });
  
  server.on("/api/led/single", HTTP_GET, []() {
    int ledIndex = server.arg("index").toInt();
    String colorStr = server.arg("color");
    
    if (ledIndex < 0 || ledIndex >= ledConfig.totalLEDs) {
      server.send(400, "text/plain", "Invalid LED index. Range: 0-" + String(ledConfig.totalLEDs - 1));
      return;
    }
    
    stopStandalonePlayback();
    
    // Clear all first
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
    
    // Set specific LED
    CRGB color = CRGB::White; // Default
    if (colorStr == "red") color = CRGB::Red;
    else if (colorStr == "green") color = CRGB::Green;
    else if (colorStr == "blue") color = CRGB::Blue;
    else if (colorStr == "yellow") color = CRGB::Yellow;
    else if (colorStr == "cyan") color = CRGB::Cyan;
    else if (colorStr == "magenta") color = CRGB::Magenta;
    
    leds[ledIndex] = color;
    FastLED.show();
    
    String colorName = colorStr;
    colorName.toUpperCase();
    server.send(200, "text/plain", "LED " + String(ledIndex) + " set to " + colorName + " (out of " + String(ledConfig.totalLEDs) + " total)");
  });
  
  server.on("/api/led/sequence", HTTP_GET, []() {
    int startIndex = server.arg("start").toInt();
    int endIndex = server.arg("end").toInt();
    String colorStr = server.arg("color");
    
    if (startIndex < 0) startIndex = 0;
    if (endIndex >= ledConfig.totalLEDs) endIndex = ledConfig.totalLEDs - 1;
    if (startIndex > endIndex) {
      server.send(400, "text/plain", "Start index must be <= end index");
      return;
    }
    
    stopStandalonePlayback();
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
    
    CRGB color = CRGB::White; // Default
    if (colorStr == "red") color = CRGB::Red;
    else if (colorStr == "green") color = CRGB::Green;
    else if (colorStr == "blue") color = CRGB::Blue;
    else if (colorStr == "yellow") color = CRGB::Yellow;
    else if (colorStr == "cyan") color = CRGB::Cyan;
    else if (colorStr == "magenta") color = CRGB::Magenta;
    
    for (int i = startIndex; i <= endIndex; i++) {
      leds[i] = color;
    }
    FastLED.show();
    
    String colorName = colorStr;
    colorName.toUpperCase();
    server.send(200, "text/plain", "LEDs " + String(startIndex) + "-" + String(endIndex) + " set to " + colorName);
  });
  
  // New endpoint: Test individual strip by index
  server.on("/api/led/strip_test", HTTP_GET, []() {
    int stripIndex = server.arg("strip").toInt();
    String colorStr = server.arg("color");
    
    if (!ledConfig.isConfigured) {
      server.send(400, "text/plain", "LED configuration not loaded from Unity yet");
      return;
    }
    
    if (stripIndex < 0 || stripIndex >= ledConfig.numSegments) {
      server.send(400, "text/plain", "Invalid strip index. Valid range: 0-" + String(ledConfig.numSegments - 1));
      return;
    }
    
    stopStandalonePlayback();
    
    // Clear all first
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
    
    // Set specific strip
    CRGB color = CRGB::White; // Default
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
  
  // Brightness control endpoints
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
  
  server.on("/api/brightness/max", HTTP_GET, []() {
    currentBrightness = 255;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: MAX (255/255)");
  });
  
  server.on("/api/brightness/high", HTTP_GET, []() {
    currentBrightness = 200;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: HIGH (200/255)");
  });
  
  server.on("/api/brightness/medium", HTTP_GET, []() {
    currentBrightness = 128;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: MEDIUM (128/255)");
  });
  
  server.on("/api/brightness/low", HTTP_GET, []() {
    currentBrightness = 64;
    FastLED.setBrightness(currentBrightness);
    FastLED.show();
    server.send(200, "text/plain", "Brightness: LOW (64/255)");
  });
  
  // Configuration management endpoints
  server.on("/api/config/clear", HTTP_GET, []() {
    clearSavedConfiguration();
    server.send(200, "text/plain", "Saved LED configuration cleared - device will use defaults until Unity sends new config");
  });
  
  server.on("/api/config/save", HTTP_GET, []() {
    if (ledConfig.isConfigured) {
      saveLEDConfiguration();
      server.send(200, "text/plain", "Current LED configuration saved to flash memory");
    } else {
      server.send(400, "text/plain", "No configuration to save - upload animations from Unity first");
    }
  });
  
  server.begin();
  Serial.println("✅ Web server started");
}

void setupOTA() {
  Serial.println("🔄 Configuring OTA updates...");
  
  ArduinoOTA.setHostname("ESP32_Animation_Player");
  ArduinoOTA.setPassword("animationplayer"); // Change this!
  
  ArduinoOTA.onStart([]() {
    String type = (ArduinoOTA.getCommand() == U_FLASH) ? "sketch" : "filesystem";
    Serial.println("🔄 OTA Update starting: " + type);
    
    // Stop playback during update
    stopStandalonePlayback();
    FastLED.clear();
    FastLED.show();
  });
  
  ArduinoOTA.onEnd([]() {
    Serial.println("\n✅ OTA Update complete");
    
    // Show completion pattern
    fill_solid(leds, ledConfig.totalLEDs, CRGB::Green);
    FastLED.show();
  });
  
  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    // Show progress as blue bar
    int ledProgress = map(progress, 0, total, 0, ledConfig.totalLEDs);
    
    FastLED.clear();
    for (int i = 0; i < ledProgress; i++) {
      leds[i] = CRGB::Blue;
    }
    FastLED.show();
  });
  
  ArduinoOTA.onError([](ota_error_t error) {
    Serial.println("❌ OTA Error[" + String(error) + "]");
    
    // Show error pattern
    for (int i = 0; i < 3; i++) {
      fill_solid(leds, ledConfig.totalLEDs, CRGB::Red);
      FastLED.show();
      delay(500);
      FastLED.clear();
      FastLED.show();
      delay(500);
    }
  });
  
  ArduinoOTA.begin();
  Serial.println("✅ OTA ready");
}

void showStartupPattern() {
  Serial.println("🎆 Showing startup pattern...");
  
  // Rainbow sweep
  for (int hue = 0; hue < 256; hue += 4) {
    for (int i = 0; i < ledConfig.totalLEDs; i++) {
      leds[i] = CHSV(hue + (i * 2), 255, 128);
    }
    FastLED.show();
    delay(20);
  }
  
  // Clear all
  FastLED.clear();
  FastLED.show();
  
  Serial.println("✅ Startup pattern complete");
}

// =================== CONFIGURATION PERSISTENCE ===================

void loadLEDConfiguration() {
  Serial.println("💾 Loading saved LED configuration...");
  
  preferences.begin("ledconfig", true); // Read-only mode
  
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
    
    Serial.println("✅ Loaded saved configuration:");
    Serial.println("   🔹 Total LEDs: " + String(ledConfig.totalLEDs));
    Serial.println("   🔹 Strips: " + String(ledConfig.numSegments));
    Serial.println("   🔹 Source: " + ledConfig.lastConfigSource);
    
    for (int i = 0; i < ledConfig.numSegments && i < 3; i++) { // Show first 3 strips
      Serial.println("   Strip " + String(i) + ": " + String(ledConfig.stripLEDs[i]) + " LEDs");
    }
    if (ledConfig.numSegments > 3) {
      Serial.println("   ... and " + String(ledConfig.numSegments - 3) + " more strips");
    }
  } else {
    Serial.println("📝 No saved configuration found - using defaults");
    Serial.println("💡 Upload animations with Unity to send configuration");
  }
  
  preferences.end();
}

void saveLEDConfiguration() {
  Serial.println("💾 Saving LED configuration to flash memory...");
  
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
  
  Serial.println("✅ LED configuration saved to flash memory");
  Serial.println("🔄 Configuration will persist through power cycles");
}

void clearSavedConfiguration() {
  Serial.println("🗑️ Clearing saved LED configuration...");
  
  preferences.begin("ledconfig", false);
  preferences.clear();
  preferences.end();
  
  // Reset to defaults
  ledConfig.totalLEDs = 100;
  ledConfig.numSegments = 1;
  ledConfig.isConfigured = false;
  ledConfig.lastConfigSource = "default";
  
  Serial.println("✅ Saved configuration cleared - using defaults");
}

// =================== ANIMATION LIBRARY FUNCTIONS ===================

void loadAnimationLibrary() {
  animationLibrary.clear();
  Serial.println("📚 Loading animation library...");
  
  File root = SPIFFS.open("/");
  if (!root) {
    Serial.println("❌ Failed to open root directory");
    return;
  }
  
  File file = root.openNextFile();
  while (file) {
    String fileName = String(file.name());
    if (!file.isDirectory() && fileName.endsWith(".anim")) {
      StoredAnimation anim = loadAnimationFromFile(fileName);
      if (anim.isValid) {
        animationLibrary.push_back(anim);
        Serial.println("  + " + anim.name + " (" + String(anim.fileSize / 1024) + "KB)");
      }
    }
    file = root.openNextFile();
  }
  
  Serial.println("✅ Loaded " + String(animationLibrary.size()) + " animations");
  
  // Load playlist if exists
  loadPlaylist();
}

StoredAnimation loadAnimationFromFile(String filename) {
  StoredAnimation anim;
  anim.filename = filename;
  anim.isValid = false;
  
  File file = SPIFFS.open("/" + filename, "r");
  if (!file) {
    return anim;
  }
  
  anim.fileSize = file.size();
  
  // Read animation header
  char magic[5];
  file.read((uint8_t*)magic, 4);
  magic[4] = '\0';
  
  if (String(magic) != "ANIM") {
    Serial.println("❌ Invalid magic header in " + filename + ": " + String(magic));
    file.close();
    return anim;
  }
  
  uint8_t version = file.read();
  if (version != 1) {
    Serial.println("❌ Unsupported version in " + filename + ": " + String(version));
    file.close();
    return anim;
  }
  
  // Read name (32 chars)
  char name[33];
  file.read((uint8_t*)name, 32);
  name[32] = '\0';
  anim.name = String(name);
  anim.name.trim();
  
  // Read animation parameters using explicit byte-by-byte reading for better control
  uint8_t durationBytes[4];
  uint8_t frameRateBytes[4];
  uint8_t ledCountBytes[4];
  uint8_t frameCountBytes[4];
  
  file.read(durationBytes, 4);
  file.read(frameRateBytes, 4);
  file.read(ledCountBytes, 4);
  file.read(frameCountBytes, 4);
  
  // Convert bytes to values (assuming little-endian from Unity)
  memcpy(&anim.duration, durationBytes, 4);
  memcpy(&anim.frameRate, frameRateBytes, 4);
  memcpy(&anim.ledCount, ledCountBytes, 4);
  memcpy(&anim.frameCount, frameCountBytes, 4);
  
  // CRITICAL: Calculate actual frame count from file size to prevent read overflow
  int bytesPerFrame = 4 + (anim.ledCount * 3); // timestamp + RGB data
  int headerSize = 53; // "ANIM" + version + name(32) + duration + frameRate + ledCount + frameCount
  int dataSize = anim.fileSize - headerSize;
  int actualFrameCount = dataSize / bytesPerFrame;
  
  if (actualFrameCount < anim.frameCount) {
    Serial.println("⚠️ Frame count mismatch! Header says " + String(anim.frameCount) + 
                   " frames, but file only has " + String(actualFrameCount) + " frames");
    anim.frameCount = actualFrameCount; // Use the safe count
  }
  
  // Debug: Show raw bytes and parsed values
  Serial.println("🔍 Raw duration bytes: " + String(durationBytes[0]) + " " + String(durationBytes[1]) + " " + String(durationBytes[2]) + " " + String(durationBytes[3]));
  Serial.println("🔍 Raw frameRate bytes: " + String(frameRateBytes[0]) + " " + String(frameRateBytes[1]) + " " + String(frameRateBytes[2]) + " " + String(frameRateBytes[3]));
  
  // Debug: Show parsed header info
  Serial.println("📋 Loaded " + anim.name + ": " + String(anim.duration, 2) + "s, " + 
                String(anim.frameRate) + "fps, " + String(anim.ledCount) + " LEDs, " + 
                String(anim.frameCount) + " frames (" + String(actualFrameCount) + " actual)");
  
  // CRITICAL DEBUG: Compare animation LED count with current ESP32 configuration
  Serial.println("🔍 LED Count Comparison:");
  Serial.println("   📁 Animation file ledCount: " + String(anim.ledCount));
  Serial.println("   🔧 ESP32 ledConfig.totalLEDs: " + String(ledConfig.totalLEDs));
  if (anim.ledCount != ledConfig.totalLEDs) {
    Serial.println("   ⚠️ MISMATCH DETECTED! Animation data may not display correctly!");
    Serial.println("   💡 This could explain the dead zones - ESP32 thinks it has " + String(ledConfig.totalLEDs) + 
                  " LEDs but animation has data for " + String(anim.ledCount) + " LEDs");
  }
  
  anim.isValid = true;
  file.close();
  
  return anim;
}

void updateStandalonePlayback() {
  if (!isPlayingStandalone || currentPlaylist.empty()) {
    return;
  }
  
  // Check if current animation has finished
  PlaylistItem& currentItem = currentPlaylist[currentPlaylistIndex];
  unsigned long animationTime = millis() - animationStartTime;
  
  if (animationTime >= currentItem.duration * 1000 / currentItem.speedMultiplier) {
    // Move to next animation
    nextPlaylistItem();
    return;
  }
  
  // Update current animation frame
  updateAnimationFrame(currentItem, animationTime);
}

void nextPlaylistItem() {
  currentPlaylistIndex++;
  
  // Check for playlist end
  if (currentPlaylistIndex >= currentPlaylist.size()) {
    if (playlistLoopEnabled) {
      currentPlaylistIndex = 0;
      Serial.println("🔄 Playlist loop - restarting");
    } else {
      stopStandalonePlayback();
      return;
    }
  }
  
  // Skip disabled items
  while (currentPlaylistIndex < currentPlaylist.size() && 
         !currentPlaylist[currentPlaylistIndex].enabled) {
    currentPlaylistIndex++;
  }
  
  if (currentPlaylistIndex >= currentPlaylist.size()) {
    if (playlistLoopEnabled) {
      currentPlaylistIndex = 0;
    } else {
      stopStandalonePlayback();
      return;
    }
  }
  
  // Start new animation
  animationStartTime = millis();
  Serial.println("🎬 Playing: " + currentPlaylist[currentPlaylistIndex].animationName);
}

void updateAnimationFrame(PlaylistItem& item, unsigned long animationTime) {
  // Find the stored animation
  StoredAnimation* animation = nullptr;
  for (auto& anim : animationLibrary) {
    if (anim.name == item.animationName) {
      animation = &anim;
      break;
    }
  }
  
  if (!animation) {
    Serial.println("⚠️ Animation not found: " + item.animationName);
    nextPlaylistItem();
    return;
  }
  
  // Calculate current frame based on animation's native frame rate
  float animationProgress = (float)animationTime / 1000.0f / item.duration * item.speedMultiplier;
  int frameIndex = (int)(animationProgress * animation->frameCount);
  
  if (frameIndex >= animation->frameCount) {
    frameIndex = animation->frameCount - 1;
  }
  
  // Frame rate limiting to match animation's original frame rate
  unsigned long targetFrameTime = 1000 / animation->frameRate; // ms per frame
  if (millis() - lastFrameTime >= targetFrameTime) {
    loadAndDisplayFrame(*animation, frameIndex);
    lastFrameTime = millis();
  }
}

void loadAndDisplayFrame(StoredAnimation& animation, int frameIndex) {
  File file = SPIFFS.open("/" + animation.filename, "r");
  if (!file) {
    Serial.println("❌ Failed to open animation file: " + animation.filename);
    return;
  }
  
  // Skip header (4 + 1 + 32 + 4 + 4 + 4 + 4 = 53 bytes)
  // Header format: "ANIM" + version + name(32) + duration + frameRate + ledCount + frameCount
  
  // Calculate frame position - each frame has: timestamp(4) + RGB data(ledCount * 3)
  int bytesPerFrame = 4 + (animation.ledCount * 3); // timestamp + RGB data
  int framePosition = 53 + (frameIndex * bytesPerFrame);
  
  // Debug file information for problematic frames
  if (frameIndex == 0 || frameIndex >= 248) {
    Serial.println("🔍 Frame " + String(frameIndex) + " debug:");
    Serial.println("  File size: " + String(file.size()) + " bytes");
    Serial.println("  Bytes per frame: " + String(bytesPerFrame) + " bytes");
    Serial.println("  Calculated position: " + String(framePosition) + " bytes");
    Serial.println("  LED count: " + String(animation.ledCount));
  }
  
  if (!file.seek(framePosition)) {
    Serial.println("❌ Failed to seek to frame " + String(frameIndex) + " at position " + String(framePosition));
    file.close();
    return;
  }
  
  // Read timestamp using explicit byte reading
  uint8_t timestampBytes[4];
  if (file.read(timestampBytes, 4) != 4) {
    Serial.println("❌ Failed to read timestamp for frame " + String(frameIndex));
    file.close();
    return;
  }
  
  float timestamp;
  memcpy(&timestamp, timestampBytes, 4);
  
  // Check for obviously corrupt timestamp values
  if (timestamp < 0 || timestamp > 1000.0) {
    Serial.println("⚠️ Suspicious timestamp for frame " + String(frameIndex) + ": " + String(timestamp, 6) + "s");
    Serial.println("🔍 Timestamp bytes: " + String(timestampBytes[0]) + " " + String(timestampBytes[1]) + " " + String(timestampBytes[2]) + " " + String(timestampBytes[3]));
  }
  
  // Debug: Show frame info occasionally and for first few frames
  if (frameIndex < 5 || frameIndex % 30 == 0) { // First 5 frames + every 30 frames
    float actualElapsed = (millis() - animationStartTime) / 1000.0f;
    Serial.println("🎬 Frame " + String(frameIndex) + "/" + String(animation.frameCount) + 
                  " | LEDs: " + String(animation.ledCount) + " | FileTime: " + String(timestamp, 2) + 
                  "s | RealTime: " + String(actualElapsed, 2) + "s");
  }
  
  // Read LED colors
  int colorsRead = 0;
  for (int i = 0; i < animation.ledCount && i < MAX_LEDS; i++) {
    uint8_t colorData[3];
    if (file.read(colorData, 3) != 3) {
      Serial.println("❌ Failed to read RGB data for LED " + String(i) + " in frame " + String(frameIndex));
      break;
    }
    
    // DEBUG: Check for problematic LED ranges that Unity reported as having issues
    bool isProblematicLED = (i >= 1 && i <= 2) ||     // P1 range
                           (i >= 17 && i <= 36) ||    // P2-P7 range (expanded)
                           (i >= 306);                // P9 last 4
    
    if (isProblematicLED && frameIndex % 60 == 0) { // Debug problematic LEDs every 60 frames
      Serial.println("🔍 ESP32 LED " + String(i) + " from file: (" + String(colorData[0]) + "," + String(colorData[1]) + "," + String(colorData[2]) + ")");
    }
    
    leds[i] = CRGB(colorData[0], colorData[1], colorData[2]);
    colorsRead++;
  }
  
  if (colorsRead != animation.ledCount) {
    Serial.println("⚠️ Only read " + String(colorsRead) + "/" + String(animation.ledCount) + " LED colors");
  }
  
  // Clear any remaining LEDs
  for (int i = animation.ledCount; i < ledConfig.totalLEDs; i++) {
    leds[i] = CRGB::Black;
  }
  
  // Debug: Show first few LED colors occasionally
  if (frameIndex % 60 == 0) { // Every 60 frames (2 seconds)
    Serial.print("🎨 First 5 LEDs: ");
    for (int i = 0; i < 5 && i < animation.ledCount; i++) {
      Serial.print("(" + String(leds[i].r) + "," + String(leds[i].g) + "," + String(leds[i].b) + ") ");
    }
    Serial.println();
  }
  
  file.close();
  FastLED.show();
}

void startStandalonePlayback() {
  if (currentPlaylist.empty()) {
    Serial.println("⚠️ No playlist loaded");
    Serial.println("📊 Debug: Animation library has " + String(animationLibrary.size()) + " items");
    for (const auto& anim : animationLibrary) {
      Serial.println("  - " + anim.name + " (" + String(anim.isValid ? "valid" : "invalid") + ")");
    }
    return;
  }
  
  Serial.println("🎭 Starting standalone playback: " + playlistName);
  Serial.println("📋 Playlist has " + String(currentPlaylist.size()) + " items:");
  for (size_t i = 0; i < currentPlaylist.size(); i++) {
    Serial.println("  " + String(i) + ": " + currentPlaylist[i].animationName + " (" + 
                  String(currentPlaylist[i].enabled ? "enabled" : "disabled") + ")");
  }
  
  isPlayingStandalone = true;
  currentPlaylistIndex = 0;
  animationStartTime = millis();
  
  // Find first enabled item
  while (currentPlaylistIndex < currentPlaylist.size() && 
         !currentPlaylist[currentPlaylistIndex].enabled) {
    currentPlaylistIndex++;
  }
  
  if (currentPlaylistIndex >= currentPlaylist.size()) {
    Serial.println("⚠️ No enabled animations in playlist");
    stopStandalonePlayback();
    return;
  }
  
  Serial.println("🎬 Starting with: " + currentPlaylist[currentPlaylistIndex].animationName);
  Serial.println("💡 LED Config: " + String(ledConfig.totalLEDs) + " LEDs, " + 
                String(ledConfig.numSegments) + " segments");
}

void stopStandalonePlayback() {
  isPlayingStandalone = false;
  Serial.println("⏹️ Stopped standalone playback");
  
  // Clear LEDs
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
  FastLED.show();
}

void loadPlaylist() {
  File file = SPIFFS.open("/playlist.json", "r");
  if (!file) {
    Serial.println("📋 No playlist found - creating default");
    createDefaultPlaylist();
    return;
  }
  
  JsonDocument doc;
  deserializeJson(doc, file);
  file.close();
  
  playlistName = doc["name"].as<String>();
  playlistLoopEnabled = doc["loop"];
  
  currentPlaylist.clear();
  JsonArray items = doc["items"];
  for (JsonObject item : items) {
    PlaylistItem playlistItem;
    playlistItem.animationName = item["name"].as<String>();
    playlistItem.duration = item["duration"];
    playlistItem.speedMultiplier = item["speed"];
    playlistItem.priority = item["priority"];
    playlistItem.enabled = item["enabled"];
    currentPlaylist.push_back(playlistItem);
  }
  
  Serial.println("📋 Loaded playlist: " + playlistName + " (" + String(currentPlaylist.size()) + " items)");
}

void createDefaultPlaylist() {
  currentPlaylist.clear();
  playlistName = "Default Playlist";
  playlistLoopEnabled = true;
  
  // Add any available animations to default playlist
  for (const auto& anim : animationLibrary) {
    PlaylistItem item;
    item.animationName = anim.name;
    item.duration = anim.duration;
    item.speedMultiplier = 1.0f;
    item.priority = 1;
    item.enabled = true;
    currentPlaylist.push_back(item);
  }
  
  if (!currentPlaylist.empty()) {
    savePlaylist();
  }
}

void savePlaylist() {
  File file = SPIFFS.open("/playlist.json", "w");
  if (!file) {
    Serial.println("❌ Failed to save playlist");
    return;
  }
  
  JsonDocument doc;
  doc["name"] = playlistName;
  doc["loop"] = playlistLoopEnabled;
  
  JsonArray items = doc["items"].to<JsonArray>();
  for (const auto& item : currentPlaylist) {
    JsonObject itemObj = items.add<JsonObject>();
    itemObj["name"] = item.animationName;
    itemObj["duration"] = item.duration;
    itemObj["speed"] = item.speedMultiplier;
    itemObj["priority"] = item.priority;
    itemObj["enabled"] = item.enabled;
  }
  
  serializeJson(doc, file);
  file.close();
  
  Serial.println("💾 Playlist saved");
}

void deleteAnimation(String animationName) {
  // Remove from library
  for (auto it = animationLibrary.begin(); it != animationLibrary.end(); ++it) {
    if (it->name == animationName) {
      // Delete file (use the actual filename which may have underscores)
      SPIFFS.remove("/" + it->filename);
      animationLibrary.erase(it);
      Serial.println("🗑️ Deleted animation: " + animationName);
      break;
    }
  }
  
  // Remove from playlist
  for (auto it = currentPlaylist.begin(); it != currentPlaylist.end(); ++it) {
    if (it->animationName == animationName) {
      currentPlaylist.erase(it);
      savePlaylist();
      break;
    }
  }
}

// =================== UDP UPLOAD HANDLING ===================

void handleUDPPackets() {
  int packetSize = udp.parsePacket();
  
  if (packetSize > 0) {
    uint8_t buffer[2048];
    int len = udp.read(buffer, sizeof(buffer));
    
    if (len >= 4 && buffer[0] == PACKET_START_MARKER) {
      uint8_t command = buffer[1];
      
      Serial.println("📨 UDP Packet: [" + String(buffer[0], HEX) + "][" + String(buffer[1], HEX) + "][" + String(buffer[2], HEX) + "][" + String(buffer[3], HEX) + "] Len:" + String(len));
      
      switch (command) {
        case COMMAND_LED_CONFIG:
          handleLEDConfigUpdate(buffer, len);
          break;
          
        case COMMAND_ANIM_UPLOAD:
          handleAnimationUpload(buffer, len);
          break;
          
        case COMMAND_PLAYLIST_UPLOAD:
          handlePlaylistUpload(buffer, len);
          break;
          
        case COMMAND_LIBRARY_STATUS:
          handleLibraryStatusRequest();
          break;
          
        case COMMAND_ANIM_PLAY:
          startStandalonePlayback();
          break;
          
        case COMMAND_ANIM_STOP:
          stopStandalonePlayback();
          break;
          
        default:
          Serial.println("❓ Unknown animation command: 0x" + String(command, HEX));
          break;
      }
    }
  }
}

void handleLEDConfigUpdate(uint8_t* buffer, int len) {
  // Parse NEW LED configuration packet with individual strip LED counts
  // Format: [0xAA][0x05][unused][unused][totalLEDs][numStrips][strip0_LEDs][strip1_LEDs]...[stripN_LEDs]
  
  Serial.println("🔧 LED Configuration packet received");
  
  if (len < 12) { // Minimum: header(4) + totalLEDs(4) + numStrips(4) = 12 bytes
    Serial.println("❌ LED config packet too short");
    return;
  }
  
  int pos = 4;
  int newTotalLEDs;
  int newNumStrips;
  
  memcpy(&newTotalLEDs, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  memcpy(&newNumStrips, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  
  // Validate configuration
  if (newTotalLEDs <= 0 || newTotalLEDs > MAX_LEDS) {
    Serial.println("❌ Invalid total LEDs: " + String(newTotalLEDs) + " (max: " + String(MAX_LEDS) + ")");
    return;
  }
  
  if (newNumStrips <= 0 || newNumStrips > 10) {
    Serial.println("❌ Invalid number of strips: " + String(newNumStrips));
    return;
  }
  
  // Check if we have enough data for all strip LED counts
  int expectedSize = 12 + (newNumStrips * 4); // header + totalLEDs + numStrips + (numStrips * 4)
  if (len < expectedSize) {
    Serial.println("❌ Packet too short for " + String(newNumStrips) + " strips. Expected " + String(expectedSize) + ", got " + String(len));
    return;
  }
  
  // Read individual strip LED counts and calculate positions
  Serial.println("📋 Individual Strip Configuration:");
  int totalCalculated = 0;
  int currentStartIndex = 0;
  
  for (int i = 0; i < newNumStrips; i++) {
    int stripLEDs;
    memcpy(&stripLEDs, &buffer[pos], sizeof(int));
    pos += sizeof(int);
    
    // Store strip information
    ledConfig.stripLEDs[i] = stripLEDs;
    ledConfig.stripStartIndices[i] = currentStartIndex;
    
    Serial.println("   Strip " + String(i) + ": " + String(stripLEDs) + " LEDs (indices " + String(currentStartIndex) + "-" + String(currentStartIndex + stripLEDs - 1) + ")");
    
    totalCalculated += stripLEDs;
    currentStartIndex += stripLEDs;
  }
  
  // Clear unused strip entries
  for (int i = newNumStrips; i < 10; i++) {
    ledConfig.stripLEDs[i] = 0;
    ledConfig.stripStartIndices[i] = 0;
  }
  
  // Verify total matches sum of individual strips
  if (totalCalculated != newTotalLEDs) {
    Serial.println("⚠️ Warning: Total LEDs (" + String(newTotalLEDs) + ") doesn't match sum of strips (" + String(totalCalculated) + ")");
  }
  
  // Update LED configuration
  ledConfig.totalLEDs = newTotalLEDs;
  ledConfig.numSegments = newNumStrips;
  ledConfig.ledsPerSegment = newTotalLEDs / newNumStrips; // Calculate average for compatibility
  ledConfig.isConfigured = true;
  ledConfig.lastConfigSource = "Unity_v2";
  
  Serial.println("✅ LED configuration updated:");
  Serial.println("   🔹 Total LEDs: " + String(ledConfig.totalLEDs));
  Serial.println("   🔹 Strips: " + String(ledConfig.numSegments));
  Serial.println("   🔹 Calculated Avg LEDs/strip: " + String(ledConfig.ledsPerSegment));
  Serial.println("   🔹 Source: " + ledConfig.lastConfigSource);
  
  // Save configuration to flash memory
  saveLEDConfiguration();
  
  // Clear current LEDs and show confirmation pattern
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Green);
  FastLED.show();
  delay(300);
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Black);
  FastLED.show();
  
  Serial.println("🎯 ESP32 is now configured for your Unity LED setup with individual strip data!");
}

void sendChunkAck(int chunkIndex) {
  if (udp.beginPacket(udp.remoteIP(), udp.remotePort())) {
    // Send acknowledgment with 16-bit chunk index: [0xAA][0x12][chunkIndex_low][chunkIndex_high]
    uint8_t ackPacket[4] = {0xAA, 0x12, (uint8_t)(chunkIndex & 0xFF), (uint8_t)((chunkIndex >> 8) & 0xFF)};
    udp.write(ackPacket, 4);
    udp.endPacket();
    
    // Debug: Show that we're sending ACKs (first few and every 10th for performance)
    if (chunkIndex < 10 || chunkIndex % 10 == 0 || chunkIndex > 250) {
      Serial.println("📤 Sent ACK for chunk " + String(chunkIndex) + " [" + String(ackPacket[2], HEX) + "][" + String(ackPacket[3], HEX) + "]");
    }
  } else {
    Serial.println("❌ Failed to send ACK for chunk " + String(chunkIndex));
  }
}

void handleAnimationUpload(uint8_t* buffer, int len) {
  // Parse animation upload packet
  // Format: [0xAA][0x10][chunkIndex_low][chunkIndex_high][totalChunks][name(32)][chunkSize][data...]
  
  Serial.println("📥 Raw packet data: " + String(buffer[0], HEX) + " " + String(buffer[1], HEX) + " " + String(buffer[2], HEX) + " " + String(buffer[3], HEX));
  Serial.println("📏 Packet length: " + String(len));
  
  if (len < 8) {
    Serial.println("❌ Animation upload packet too short");
    return;
  }
  
  // Read chunk index as 16-bit value (little-endian)
  int chunkIndex = buffer[2] | (buffer[3] << 8);
  
  // Read header from packet
  int pos = 4;
  int totalChunks;
  memcpy(&totalChunks, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  
  char animName[33];
  memcpy(animName, &buffer[pos], 32);
  animName[32] = '\0';
  pos += 32;
  
  int chunkSize;
  memcpy(&chunkSize, &buffer[pos], sizeof(int));
  
  Serial.println("🔍 Parsed: chunk=" + String(chunkIndex) + ", total=" + String(totalChunks) + ", name='" + String(animName) + "', size=" + String(chunkSize));
  pos += sizeof(int);
  
  String animationName = String(animName);
  animationName.trim();
  
  Serial.println("📦 Animation upload chunk " + String(chunkIndex + 1) + "/" + String(totalChunks) + 
                " for '" + animationName + "' (" + String(chunkSize) + " bytes)");
  
  // Initialize upload ONLY if:
  // 1. This is chunk 0 (first chunk) AND no upload is in progress, OR
  // 2. This is chunk 0 (first chunk) AND it's a different animation
  if (chunkIndex == 0 && (!currentUpload.inProgress || currentUpload.name != animationName)) {
    Serial.println("🔄 Initializing upload for: " + animationName + " (chunk " + String(chunkIndex) + ")");
    
    // Close any previous upload
    if (currentUpload.uploadFile) {
      currentUpload.uploadFile.close();
    }
    
    if (currentUpload.inProgress && currentUpload.uploadFile) {
      // Close previous upload
      currentUpload.uploadFile.close();
      Serial.println("⚠️ Closed previous incomplete upload");
    }
    
    currentUpload.name = animationName;
    currentUpload.totalChunks = totalChunks;
    currentUpload.receivedChunks = 0;
    currentUpload.inProgress = true;
    currentUpload.lastChunkTime = millis();
    
    // Create new file (flat structure - SPIFFS doesn't support subdirectories)
    String safeAnimName = animationName;
    safeAnimName.replace(" ", "_");
    String filename = "/" + safeAnimName + ".anim";
    
    Serial.println("📂 Creating file: " + filename);
    
    // Check SPIFFS space before creating file
    size_t freeBytes = SPIFFS.totalBytes() - SPIFFS.usedBytes();
    uint32_t estimatedSize = (totalChunks - 1) * 512 + 512; // More accurate: most chunks are 512B, last chunk ≤ 512B
    Serial.println("💾 Available space: " + String(freeBytes) + " bytes (need ~" + String(estimatedSize) + " bytes)");
    
    if (freeBytes < estimatedSize) {
      Serial.println("❌ Insufficient SPIFFS space! Need ~" + String(estimatedSize) + " bytes, have " + String(freeBytes));
      currentUpload.inProgress = false;
      return;
    }
    
    currentUpload.uploadFile = SPIFFS.open(filename, "w");
    
    if (!currentUpload.uploadFile) {
      Serial.println("❌ Failed to create animation file: " + filename);
      
      // Try to create a test file to diagnose the issue
      File testFile = SPIFFS.open("/test.tmp", "w");
      if (testFile) {
        testFile.print("test");
        testFile.close();
        SPIFFS.remove("/test.tmp");
        Serial.println("🔍 SPIFFS write test: SUCCESS - issue may be with filename");
      } else {
        Serial.println("🔍 SPIFFS write test: FAILED - filesystem may be corrupted");
      }
      
      currentUpload.inProgress = false;
      return;
    }
    
    Serial.println("✅ File created successfully");
    
    Serial.println("🆕 Animation upload initialized: " + animationName + " (" + String(totalChunks) + " chunks expected)");
  }
  
  // Check if upload is properly initialized
  if (!currentUpload.inProgress || !currentUpload.uploadFile) {
    Serial.println("⚠️ Received chunk " + String(chunkIndex) + " but no upload in progress - skipping");
    return;
  }
  
  // Verify this chunk belongs to the current upload
  if (currentUpload.name != animationName) {
    Serial.println("⚠️ Chunk for '" + animationName + "' but current upload is '" + currentUpload.name + "' - skipping");
    return;
  }
  
  // Write chunk data
  int dataSize = len - pos;
  if (dataSize > 0) {
    currentUpload.uploadFile.write(&buffer[pos], dataSize);
    currentUpload.receivedChunks++;
    currentUpload.lastChunkTime = millis();
    
    Serial.println("📈 Progress: " + String(currentUpload.receivedChunks) + "/" + String(currentUpload.totalChunks) + " chunks received (chunk #" + String(chunkIndex) + ")");
    
    // Send acknowledgment back to Unity
    sendChunkAck(chunkIndex);
  }
  
  // Check if this is the last chunk and we have sufficient data for completion
  bool gotLastChunk = (chunkIndex >= currentUpload.totalChunks - 1);
  float completionRate = (float)currentUpload.receivedChunks / currentUpload.totalChunks;
  bool nearComplete = (completionRate >= 0.98); // 98% completion - with ACK system we should get near 100%
  bool sufficientData = (completionRate >= 0.90); // At least 90% of chunks - missing chunks corrupt playback
  
  // Only complete if we got the last chunk AND have high completion rate AND sufficient data
  if (gotLastChunk && nearComplete && sufficientData) {
    currentUpload.uploadFile.close();
    currentUpload.inProgress = false;
    
    Serial.println("✅ Animation upload complete: " + animationName + " (" + String(currentUpload.receivedChunks) + "/" + String(currentUpload.totalChunks) + " chunks, " + String((float)currentUpload.receivedChunks/currentUpload.totalChunks*100, 1) + "%)");
    
    // Verify file integrity
    String safeAnimName = animationName;
    safeAnimName.replace(" ", "_");
    File verifyFile = SPIFFS.open("/" + safeAnimName + ".anim", "r");
    if (verifyFile) {
      size_t actualSize = verifyFile.size();
      size_t expectedSize = (currentUpload.totalChunks - 1) * 512 + 512; // More accurate estimate
      verifyFile.close();
      
      if (actualSize < expectedSize * 0.8) { // Allow 20% variance for final chunk size
        Serial.println("⚠️ File may be incomplete: " + String(actualSize) + " bytes (expected ~" + String(expectedSize) + ")");
      } else {
        Serial.println("✅ File integrity check passed: " + String(actualSize) + " bytes");
      }
    }
    
    // Reload animation library
    loadAnimationLibrary();
  }
  // Note: Upload timeout and cleanup is handled in loop() function
}

void handlePlaylistUpload(uint8_t* buffer, int len) {
  // Parse playlist upload packet
  // Format: [0xAA][0x11][unused][dataLength][jsonLength][jsonData...]
  
  int pos = 4;
  int dataLength;
  memcpy(&dataLength, &buffer[pos], sizeof(int));
  pos += sizeof(int);
  
  if (dataLength <= 0 || pos + dataLength > len) {
    Serial.println("❌ Invalid playlist data length");
    return;
  }
  
  // Extract JSON data
  String jsonData = "";
  for (int i = 0; i < dataLength; i++) {
    jsonData += (char)buffer[pos + i];
  }
  
  Serial.println("📋 Received playlist data (" + String(dataLength) + " bytes)");
  
  // Parse and save playlist
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonData);
  
  if (error) {
    Serial.println("❌ Failed to parse playlist JSON: " + String(error.c_str()));
    return;
  }
  
  // Update current playlist
  playlistName = doc["name"].as<String>();
  playlistLoopEnabled = doc["loop"];
  
  currentPlaylist.clear();
  JsonArray items = doc["items"];
  for (JsonObject item : items) {
    PlaylistItem playlistItem;
    playlistItem.animationName = item["name"].as<String>();
    playlistItem.duration = item["duration"];
    playlistItem.speedMultiplier = item["speed"];
    playlistItem.priority = item["priority"];
    playlistItem.enabled = item["enabled"];
    currentPlaylist.push_back(playlistItem);
  }
  
  // Save playlist to file
  savePlaylist();
  
  Serial.println("✅ Playlist updated: " + playlistName + " (" + String(currentPlaylist.size()) + " items)");
}

void handleLibraryStatusRequest() {
  Serial.println("📊 Animation library status requested");
  
  // Send status response (simplified for now)
  String status = "📚 Animation Library Status:\n";
  status += "- Total animations: " + String(animationLibrary.size()) + "\n";
  status += "- Current playlist: " + playlistName + " (" + String(currentPlaylist.size()) + " items)\n";
  status += "- Playing: " + String(isPlayingStandalone ? "Yes" : "No") + "\n";
  
  size_t totalBytes = SPIFFS.totalBytes();
  size_t usedBytes = SPIFFS.usedBytes();
  float usagePercent = (float)usedBytes / totalBytes * 100;
  
  status += "- Storage: " + String(usedBytes / 1024) + "KB / " + String(totalBytes / 1024) + "KB (" + String(usagePercent, 1) + "%)\n";
  
  Serial.println(status);
}

// =================== WEB INTERFACE ===================

String generateMainPage() {
  String html = R"html(
<!DOCTYPE html>
<html>
<head>
<title>ESP32 Animation Player</title>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<style>
body { font-family: Arial, sans-serif; margin: 20px; background-color: #f0f0f0; }
.container { max-width: 1000px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
h1 { color: #333; text-align: center; }
.status { display: flex; justify-content: space-between; margin: 20px 0; }
.status-item { text-align: center; padding: 10px; background: #f8f9fa; border-radius: 5px; min-width: 120px; }
.playing { color: #28a745; }
.stopped { color: #dc3545; }
.controls { margin: 20px 0; }
.btn { display: inline-block; margin: 5px; padding: 10px 15px; background: #007bff; color: white; text-decoration: none; border-radius: 5px; border: none; cursor: pointer; }
.btn:hover { background: #0056b3; }
.btn.danger { background: #dc3545; }
.btn.danger:hover { background: #c82333; }
.animations { margin: 20px 0; }
.animation-item { display: flex; justify-content: space-between; align-items: center; padding: 10px; border: 1px solid #ddd; margin: 5px 0; border-radius: 5px; }
.playlist { margin: 20px 0; }
.storage { background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0; }
</style>
<script>
function refreshStatus() {
  fetch('/api/storage').then(r => r.json()).then(data => {
    document.getElementById('used-storage').textContent = data.usedKB + 'KB';
    document.getElementById('total-storage').textContent = data.totalKB + 'KB';
    document.getElementById('usage-percent').textContent = data.usagePercent + '%';
  });
  
  // Also update LED configuration info
  fetch('/api/ledinfo').then(r => r.json()).then(data => {
    // Update LED count displays
    document.getElementById('total-leds').textContent = data.totalLEDs;
    document.getElementById('max-led-index').textContent = data.totalLEDs - 1;
    
    // Update configuration info
    document.getElementById('led-config-source').textContent = data.configSource;
    document.getElementById('led-status').textContent = data.isConfigured ? '✅ Configured' : '⚠️ Default';
    
    // Update all input field max values to use actual LED count
    document.getElementById('led-index').max = data.totalLEDs - 1;
    document.getElementById('range-start').max = data.totalLEDs - 1;
    document.getElementById('range-end').max = data.totalLEDs - 1;
    document.getElementById('walker-start').max = data.totalLEDs - 1;
    
    // Update walker count max to reasonable portion of total LEDs
    const maxWalkerCount = Math.min(100, Math.max(10, Math.floor(data.totalLEDs / 2)));
    document.getElementById('walker-count').max = maxWalkerCount;
    
    // Update range-end default if it's still at old default
    const rangeEnd = document.getElementById('range-end');
    if (parseInt(rangeEnd.value) === 9) {
      rangeEnd.value = Math.min(9, data.totalLEDs - 1);
    }
    
    // Update strip information if available
    if (data.strips && data.strips.length > 0) {
      updateStripInfo(data.strips);
    }
  }).catch(err => {
    console.log('Error fetching LED info:', err);
  });
}

function toggleLoop() {
  const checkbox = document.getElementById('loop-checkbox');
  fetch('/api/playlist/loop?enable=' + checkbox.checked)
    .then(r => r.text())
    .then(data => console.log(data));
}

function togglePlaylistItem(index) {
  fetch('/api/playlist/toggle?index=' + index)
    .then(r => r.text())
    .then(data => {
      console.log(data);
      setTimeout(() => location.reload(), 500); // Refresh to show changes
    });
}

function showSystemInfo() {
  fetch('/api/logs')
    .then(r => r.text())
    .then(data => {
      alert(data);
    })
    .catch(err => {
      console.error('Error fetching system info:', err);
      alert('Error fetching system info: ' + err);
    });
}

// Load data when page loads
document.addEventListener('DOMContentLoaded', function() {
  refreshStatus();
  setInterval(refreshStatus, 5000);
});

// Handle play button click
function playAnimation() {
  fetch('/api/play')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      setTimeout(() => location.reload(), 500); // Refresh to show changes
    })
    .catch(err => {
      console.error('Error playing animation:', err);
      alert('Error starting playback: ' + err);
    });
  return false; // Prevent default link behavior
}

function stopAnimation() {
  fetch('/api/stop')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      setTimeout(() => location.reload(), 500); // Refresh to show changes
    })
    .catch(err => {
      console.error('Error stopping animation:', err);
      alert('Error stopping playback: ' + err);
    });
  return false; // Prevent default link behavior
}

function nextAnimation() {
  fetch('/api/next')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      setTimeout(() => location.reload(), 500); // Refresh to show changes
    })
    .catch(err => {
      console.error('Error switching animation:', err);
      alert('Error switching to next animation: ' + err);
    });
  return false; // Prevent default link behavior
}

// Brightness control functions
function updateBrightness(value) {
  document.getElementById('brightness-value').textContent = value;
  setBrightness(parseInt(value));
}

function setBrightness(brightness) {
  let value;
  // Handle preset strings
  if (brightness === 'max') value = 255;
  else if (brightness === 'high') value = 200;
  else if (brightness === 'medium') value = 128;
  else if (brightness === 'low') value = 64;
  else value = parseInt(brightness);
  
  fetch('/api/brightness?value=' + value)
    .then(r => r.text())
    .then(data => {
      console.log('Brightness set to:', value);
      // Update slider and display value
      const slider = document.getElementById('brightness-slider');
      const display = document.getElementById('brightness-value');
      if (slider) slider.value = value;
      if (display) display.textContent = value;
    })
    .catch(err => {
      console.error('Error setting brightness:', err);
      alert('Error setting brightness: ' + err);
    });
}

function testLEDs() {
  if (!confirm('This will test your LED strip. Continue?')) {
    return;
  }
  
  fetch('/api/ledtest')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      alert('LED test completed! Check your LED strip.');
    })
    .catch(err => {
      console.error('Error testing LEDs:', err);
      alert('Error testing LEDs: ' + err);
    });
}

function showDebugLogs() {
  window.open('/api/logs', '_blank');
}

// Playlist management functions
function addToPlaylist(animName) {
  fetch('/api/playlist/add?name=' + encodeURIComponent(animName))
    .then(r => r.text())
    .then(data => {
      console.log(data);
      if (data.includes('added')) {
        alert('✅ Added "' + animName + '" to playlist');
        setTimeout(() => location.reload(), 500);
      } else {
        alert('❌ ' + data);
      }
    })
    .catch(err => {
      console.error('Error adding to playlist:', err);
      alert('Error adding to playlist: ' + err);
    });
}

function removeFromPlaylist(index) {
  if (!confirm('Remove this animation from the playlist?')) {
    return;
  }
  
  fetch('/api/playlist/remove?index=' + index)
    .then(r => r.text())
    .then(data => {
      console.log(data);
      if (data.includes('Removed')) {
        alert('✅ ' + data);
        setTimeout(() => location.reload(), 500);
      } else {
        alert('❌ ' + data);
      }
    })
    .catch(err => {
      console.error('Error removing from playlist:', err);
      alert('Error removing from playlist: ' + err);
    });
}

function clearPlaylist() {
  if (!confirm('Clear the entire playlist? This will stop playback if running.')) {
    return;
  }
  
  fetch('/api/playlist/clear')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      alert('✅ ' + data);
      setTimeout(() => location.reload(), 500);
    })
    .catch(err => {
      console.error('Error clearing playlist:', err);
      alert('Error clearing playlist: ' + err);
    });
}

function createDefaultPlaylist() {
  fetch('/api/playlist/create_default')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      alert('✅ ' + data);
      setTimeout(() => location.reload(), 500);
    })
    .catch(err => {
      console.error('Error creating default playlist:', err);
      alert('Error creating default playlist: ' + err);
    });
}

function deleteAnimation(animName) {
  if (!confirm('Delete animation "' + animName + '"? This cannot be undone.')) {
    return;
  }
  
  fetch('/api/delete?name=' + encodeURIComponent(animName))
    .then(r => r.text())
    .then(data => {
      console.log(data);
      alert('✅ Animation deleted');
      setTimeout(() => location.reload(), 500);
    })
    .catch(err => {
      console.error('Error deleting animation:', err);
      alert('Error deleting animation: ' + err);
    });
}

// LED Debugging Functions
function setAllLEDs(color) {
  const endpoint = color === 'black' ? '/api/led/all_black' : '/api/led/all_white';
  
  fetch(endpoint)
    .then(r => r.text())
    .then(data => {
      console.log(data);
      // Don't show alert for basic on/off commands
    })
    .catch(err => {
      console.error('Error setting LEDs:', err);
      alert('Error setting LEDs: ' + err);
    });
}

function showRainbow() {
  fetch('/api/led/rainbow')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      // Don't show alert for pattern commands
    })
    .catch(err => {
      console.error('Error showing rainbow:', err);
      alert('Error showing rainbow: ' + err);
    });
}

function showColorSections() {
  fetch('/api/led/color_test')
    .then(r => r.text())
    .then(data => {
      console.log(data);
      // Don't show alert for pattern commands
    })
    .catch(err => {
      console.error('Error showing color sections:', err);
      alert('Error showing color sections: ' + err);
    });
}

function setSingleLED() {
  const index = document.getElementById('led-index').value;
  const color = document.getElementById('led-color').value;
  
  fetch('/api/led/single?index=' + index + '&color=' + color)
    .then(r => r.text())
    .then(data => {
      console.log(data);
      // Show response for single LED to confirm which LED was set
      document.getElementById('debug-status').textContent = data;
    })
    .catch(err => {
      console.error('Error setting single LED:', err);
      alert('Error setting single LED: ' + err);
    });
}

function setLEDRange() {
  const start = document.getElementById('range-start').value;
  const end = document.getElementById('range-end').value;
  const color = document.getElementById('range-color').value;
  
  fetch('/api/led/sequence?start=' + start + '&end=' + end + '&color=' + color)
    .then(r => r.text())
    .then(data => {
      console.log(data);
      document.getElementById('debug-status').textContent = data;
    })
    .catch(err => {
      console.error('Error setting LED range:', err);
      alert('Error setting LED range: ' + err);
    });
}

// LED Walker functionality
let walkerInterval = null;
let walkerCurrentIndex = 0;

// Strip information
function updateStripInfo(strips) {
  const stripInfoElement = document.getElementById('strip-info');
  if (!stripInfoElement) return;
  
  let html = '<h5>Individual Strip Mapping:</h5>';
  html += '<div style="max-height: 150px; overflow-y: auto; font-size: 12px;">';
  
  for (let i = 0; i < strips.length; i++) {
    const strip = strips[i];
    html += '<div style="margin: 2px 0; padding: 3px; background: #f5f5f5; border-radius: 3px;">';
    html += '<strong>Strip ' + strip.index + ':</strong> ' + strip.ledCount + ' LEDs ';
    html += '(indices ' + strip.startIndex + '-' + strip.endIndex + ') ';
    html += '<button onclick="testStrip(' + strip.index + ')" style="font-size: 10px; padding: 2px 6px; margin-left: 5px;">Test</button>';
    html += '</div>';
  }
  
  html += '</div>';
  stripInfoElement.innerHTML = html;
}

function testStrip(stripIndex) {
  fetch('/api/led/strip_test?strip=' + stripIndex + '&color=white')
    .then(r => r.text())
    .then(data => {
      document.getElementById('debug-status').textContent = data;
    })
    .catch(err => {
      console.error('Error testing strip:', err);
      alert('Error testing strip: ' + err);
    });
}

function startLEDWalker() {
  if (walkerInterval) {
    clearInterval(walkerInterval);
  }
  
  const startIndex = parseInt(document.getElementById('walker-start').value);
  const count = parseInt(document.getElementById('walker-count').value);
  const speed = document.getElementById('walker-speed').value;
  
  // Get current LED configuration to validate bounds
  const totalLEDs = parseInt(document.getElementById('total-leds').textContent);
  
  // Validate inputs
  if (startIndex < 0 || startIndex >= totalLEDs) {
    document.getElementById('debug-status').textContent = 'LED Walker: Invalid start index ' + startIndex + ' (max: ' + (totalLEDs - 1) + ')';
    return;
  }
  
  if (count < 1) {
    document.getElementById('debug-status').textContent = 'LED Walker: Count must be at least 1';
    return;
  }
  
  // Adjust count if it would exceed total LEDs
  const maxCount = totalLEDs - startIndex;
  const actualCount = Math.min(count, maxCount);
  if (actualCount !== count) {
    document.getElementById('debug-status').textContent = 'LED Walker: Adjusted count from ' + count + ' to ' + actualCount + ' to fit within LED range';
    document.getElementById('walker-count').value = actualCount;
  }
  
  let delay = 500; // medium
  if (speed === 'fast') delay = 200;
  else if (speed === 'slow') delay = 1000;
  
  walkerCurrentIndex = startIndex;
  const endIndex = startIndex + actualCount - 1;
  
  document.getElementById('debug-status').textContent = 'LED Walker: Walking LEDs ' + startIndex + '-' + endIndex + ' (' + actualCount + ' LEDs)';
  
  walkerInterval = setInterval(() => {
    // Set current LED
    fetch('/api/led/single?index=' + walkerCurrentIndex + '&color=white')
      .then(r => r.text())
      .then(data => {
        document.getElementById('debug-status').textContent = 'LED Walker: LED ' + walkerCurrentIndex + ' of ' + startIndex + '-' + endIndex;
      });
    
    walkerCurrentIndex++;
    if (walkerCurrentIndex > endIndex) {
      walkerCurrentIndex = startIndex; // Loop back
    }
  }, delay);
}

function stopLEDWalker() {
  if (walkerInterval) {
    clearInterval(walkerInterval);
    walkerInterval = null;
    document.getElementById('debug-status').textContent = 'LED Walker: Stopped';
    
    // Turn off all LEDs
    fetch('/api/led/all_black');
  }
}
</script>
</head>
<body>
<div class="container">
<h1>🎬 ESP32 Animation Player</h1>

<div class="status">
<div class="status-item">
<strong>Status</strong><br>
<span class=")html" + String(isPlayingStandalone ? "playing\">🎭 Playing" : "stopped\">⏹️ Stopped") + R"html(</span>
</div>
<div class="status-item">
<strong>Animations</strong><br>
<span>)html" + String(animationLibrary.size()) + R"html(</span>
</div>
<div class="status-item">
<strong>Playlist Items</strong><br>
<span>)html" + String(currentPlaylist.size()) + R"html(</span>
</div>
<div class="status-item">
<strong>Storage Used</strong><br>
<span id="used-storage">Loading...</span>
</div>
</div>

<div class="controls">
<h3>Playback Controls</h3>
<button onclick="playAnimation()" class="btn">▶️ Play</button>
<button onclick="stopAnimation()" class="btn">⏹️ Stop</button>
<button onclick="nextAnimation()" class="btn">⏭️ Next</button>
<button onclick="location.reload()" class="btn">🔄 Refresh</button>
</div>

<div class="controls">
<h3>LED Brightness</h3>
<div style="display: flex; align-items: center; gap: 10px; margin-bottom: 10px;">
<label for="brightness-slider">Brightness: </label>
<input type="range" id="brightness-slider" min="1" max="255" value=")html" + String(currentBrightness) + R"html(" 
       oninput="updateBrightness(this.value)" style="flex: 1;">
<span id="brightness-value">)html" + String(currentBrightness) + R"html(</span>
</div>
<button onclick="setBrightness(64)" class="btn" style="font-size: 12px;">🌙 Dim (25%)</button>
<button onclick="setBrightness(128)" class="btn" style="font-size: 12px;">🌅 Medium (50%)</button>
<button onclick="setBrightness(192)" class="btn" style="font-size: 12px;">☀️ Bright (75%)</button>
<button onclick="setBrightness(255)" class="btn" style="font-size: 12px;">🔆 Max (100%)</button>
</div>



<div class="animations">
<h3>Animation Library</h3>)html";

  // Add animation list
  for (const auto& anim : animationLibrary) {
    // Check if this animation is already in the playlist
    bool inPlaylist = false;
    for (const auto& item : currentPlaylist) {
      if (item.animationName == anim.name) {
        inPlaylist = true;
        break;
      }
    }
    
    html += R"html(<div class="animation-item">
<div>
<strong>)html" + anim.name + R"html(</strong><br>
<small>Duration: )html" + String(anim.duration, 1) + R"html(s | Size: )html" + String(anim.fileSize / 1024) + R"html(KB | Frames: )html" + String(anim.frameCount) + R"html(</small>
</div>
<div>)html";
    
    if (!inPlaylist) {
      html += R"html(<button onclick="addToPlaylist(')html" + anim.name + R"html(')" class="btn" style="font-size: 12px;">➕ Add to Playlist</button> )html";
    } else {
      html += R"html(<span class="btn" style="font-size: 12px; background: #6c757d; cursor: default;">✅ In Playlist</span> )html";
    }
    
    html += R"html(<button onclick="deleteAnimation(')html" + anim.name + R"html(')" class="btn danger" style="font-size: 12px;">🗑️ Delete</button>
</div>
</div>)html";
  }

  html += R"html(</div>

<div class="playlist">
<h3>Current Playlist: )html" + playlistName + R"html( ()html" + String(currentPlaylist.size()) + R"html( items)</h3>
<div class="playlist-controls">
<label><input type="checkbox" id="loop-checkbox" )html" + String(playlistLoopEnabled ? "checked" : "") + R"html(" onchange="toggleLoop()"> Loop Playlist</label>
<button onclick="createDefaultPlaylist()" class="btn" style="margin-left: 10px;">🎵 Add All Animations</button>
<button onclick="clearPlaylist()" class="btn danger" style="margin-left: 5px;">🗑️ Clear Playlist</button>
</div>)html";

  // Add playlist items
  for (size_t i = 0; i < currentPlaylist.size(); i++) {
    const auto& item = currentPlaylist[i];
    String activeClass = (isPlayingStandalone && i == currentPlaylistIndex) ? " style=\"background-color: #e7f3ff;\"" : "";
    String disabledClass = !item.enabled ? " style=\"opacity: 0.6;\"" : "";
    html += R"html(<div class="animation-item")html" + activeClass + disabledClass + R"html(">
<div>
<strong>)html" + item.animationName + R"html(</strong><br>
<small>Duration: )html" + String(item.duration, 1) + R"html(s | Speed: )html" + String(item.speedMultiplier, 1) + R"html(x | )html" + String(item.enabled ? "Enabled" : "Disabled") + R"html(</small>
</div>
<div>
<button class="btn" onclick="togglePlaylistItem()html" + String(i) + R"html()" style="font-size: 12px;">)html" + String(item.enabled ? "🔇 Disable" : "🔊 Enable") + R"html(</button>
<button class="btn danger" onclick="removeFromPlaylist()html" + String(i) + R"html()" style="font-size: 12px; margin-left: 5px;">❌ Remove</button>
</div>
</div>)html";
  }

  html += R"html(</div>

<!-- LED Debugging & Testing Section -->
<div class="controls">
<h2>🔧 LED Debugging & Testing</h2>

<div style="display: flex; justify-content: space-between; margin: 15px 0; padding: 10px; background-color: #e8f4f8; border-radius: 5px;">
<div><strong>Total LEDs:</strong> <span id="total-leds">)html" + String(ledConfig.totalLEDs) + R"html(</span></div>
<div><strong>Configuration:</strong> <span id="led-config-source">)html" + ledConfig.lastConfigSource + R"html(</span></div>
<div><strong>Status:</strong> <span id="led-status">)html" + String(ledConfig.isConfigured ? "✅ Configured" : "⚠️ Default") + R"html(</span></div>
</div>

<!-- Quick Pattern Tests -->
<div style="margin: 15px 0;">
<h4>🎨 Pattern Tests</h4>
<button onclick="setAllLEDs('white')" class="btn">⚪ All White</button>
<button onclick="setAllLEDs('black')" class="btn">⚫ All Off</button>
<button onclick="showRainbow()" class="btn">🌈 Rainbow</button>
<button onclick="showColorSections()" class="btn">🎨 RGB Sections</button>
<button onclick="testLEDs()" class="btn">🧪 Full Test Suite</button>
</div>

<!-- Brightness Controls -->
<div style="margin: 15px 0;">
<h4>💡 Brightness Controls (Current: )html" + String(currentBrightness) + R"html(/255)</h4>
<button onclick="setBrightness('max')" class="btn">🔆 MAX</button>
<button onclick="setBrightness('high')" class="btn">🔅 HIGH</button>
<button onclick="setBrightness('medium')" class="btn">🌗 MEDIUM</button>
<button onclick="setBrightness('low')" class="btn">🌑 LOW</button>
<div style="margin-top: 10px;">
<label>Custom Brightness (0-255): 
<input type="range" id="brightness-slider" min="0" max="255" value=")html" + String(currentBrightness) + R"html(" onchange="updateBrightness(this.value)" style="width: 200px; margin: 0 10px;">
<span id="brightness-value">)html" + String(currentBrightness) + R"html(</span>
</label>
</div>
</div>

<!-- Individual LED Control -->
<div style="margin: 15px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; background-color: #fafafa;">
<h4>💡 Individual LED Control</h4>
<div style="margin: 10px 0;">
<label>LED Index (0-<span id="max-led-index">)html" + String(ledConfig.totalLEDs - 1) + R"html(</span>): 
<input type="number" id="led-index" min="0" max=")html" + String(ledConfig.totalLEDs - 1) + R"html(" value="0" style="width: 80px; margin: 0 5px; padding: 5px;"></label>
<select id="led-color" style="margin: 0 5px; padding: 5px;">
<option value="white">White</option>
<option value="red">Red</option>
<option value="green">Green</option>
<option value="blue">Blue</option>
<option value="yellow">Yellow</option>
<option value="cyan">Cyan</option>
<option value="magenta">Magenta</option>
</select>
<button onclick="setSingleLED()" class="btn">💡 Light LED</button>
</div>
</div>

<!-- LED Range Control -->
<div style="margin: 15px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; background-color: #fafafa;">
<h4>🎯 LED Range Control</h4>
<div style="margin: 10px 0;">
<label>Start: <input type="number" id="range-start" min="0" max=")html" + String(ledConfig.totalLEDs - 1) + R"html(" value="0" style="width: 70px; margin: 0 5px; padding: 5px;"></label>
<label>End: <input type="number" id="range-end" min="0" max=")html" + String(ledConfig.totalLEDs - 1) + R"html(" value=")html" + String(min(9, ledConfig.totalLEDs - 1)) + R"html(" style="width: 70px; margin: 0 5px; padding: 5px;"></label>
<select id="range-color" style="margin: 0 5px; padding: 5px;">
<option value="white">White</option>
<option value="red">Red</option>
<option value="green">Green</option>
<option value="blue">Blue</option>
<option value="yellow">Yellow</option>
<option value="cyan">Cyan</option>
<option value="magenta">Magenta</option>
</select>
<button onclick="setLEDRange()" class="btn">🎯 Light Range</button>
</div>
</div>

<!-- LED Walker -->
<div style="margin: 15px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; background-color: #fafafa;">
<h4>🚶 Sequential LED Walker</h4>
<p style="margin: 5px 0; font-size: 14px; color: #666;"><em>Lights up LEDs one by one to help identify physical positions</em></p>
<div style="margin: 10px 0;">
<label>Start: <input type="number" id="walker-start" min="0" max=")html" + String(ledConfig.totalLEDs - 1) + R"html(" value="0" style="width: 70px; margin: 0 5px; padding: 5px;"></label>
<label>Count: <input type="number" id="walker-count" min="1" max=")html" + String(min(100, ledConfig.totalLEDs)) + R"html(" value=")html" + String(min(10, ledConfig.totalLEDs)) + R"html(" style="width: 70px; margin: 0 5px; padding: 5px;"></label>
<label>Speed: <select id="walker-speed" style="margin: 0 5px; padding: 5px;">
<option value="fast">Fast (200ms)</option>
<option value="medium" selected>Medium (500ms)</option>
<option value="slow">Slow (1000ms)</option>
</select></label>
<button onclick="startLEDWalker()" class="btn">🚶 Start Walker</button>
<button onclick="stopLEDWalker()" class="btn">⏹️ Stop</button>
</div>
</div>

<!-- Debug Status -->
<div style="margin: 15px 0; padding: 15px; background-color: #f8f9fa; border-radius: 5px; border-left: 4px solid #007bff;">
<h4>📊 Debug Status</h4>
<p id="debug-status" style="margin: 5px 0; font-family: monospace; font-size: 13px; color: #333;">Ready for LED debugging...</p>
</div>

<!-- System Information -->
<div style="margin: 15px 0; padding: 15px; background-color: #f8f9fa; border-radius: 5px;">
<h4>ℹ️ System Information</h4>
<button onclick="showDebugLogs()" class="btn">📋 Debug Logs</button>
<button onclick="showSystemInfo()" class="btn">ℹ️ System Info</button>
<div style="margin-top: 10px; font-size: 13px;">
<p><strong>Total Storage:</strong> <span id="total-storage">Loading...</span> | <strong>Used:</strong> <span id="used-storage">Loading...</span> (<span id="usage-percent">Loading...</span>)</p>
<p><strong>IP Address:</strong> )html" + WiFi.localIP().toString() + R"html( | <strong>Uptime:</strong> )html" + String(millis() / 1000) + R"html( seconds</p>
</div>

<!-- Strip Mapping Information -->
<div id="strip-info" style="margin-top: 15px; padding: 10px; background-color: #ffffff; border: 1px solid #ddd; border-radius: 5px;">
<h5>Strip Configuration:</h5>
<p style="font-size: 12px; color: #666;">Strip mapping will appear here after Unity uploads LED configuration</p>
</div>
</div>
</div>



</div>
</body>
</html>)html";

  return html;
}

String generateStorageJSON() {
  size_t totalBytes = SPIFFS.totalBytes();
  size_t usedBytes = SPIFFS.usedBytes();
  float usagePercent = (float)usedBytes / totalBytes * 100;
  
  return "{\"totalKB\":" + String(totalBytes / 1024) + 
         ",\"usedKB\":" + String(usedBytes / 1024) + 
         ",\"usagePercent\":" + String(usagePercent, 1) + "}";
}

String generateAnimationsJSON() {
  String json = "[";
  for (size_t i = 0; i < animationLibrary.size(); i++) {
    if (i > 0) json += ",";
    const auto& anim = animationLibrary[i];
    json += "{\"name\":\"" + anim.name + "\",\"duration\":" + String(anim.duration) + 
           ",\"frames\":" + String(anim.frameCount) + ",\"size\":" + String(anim.fileSize) + "}";
  }
  json += "]";
  return json;
}

String generateLEDInfoJSON() {
  String json = "{";
  json += "\"totalLEDs\":" + String(ledConfig.totalLEDs) + ",";
  json += "\"numSegments\":" + String(ledConfig.numSegments) + ",";
  json += "\"ledsPerSegment\":" + String(ledConfig.ledsPerSegment) + ",";
  json += "\"isConfigured\":" + String(ledConfig.isConfigured ? "true" : "false") + ",";
  json += "\"configSource\":\"" + ledConfig.lastConfigSource + "\",";
  json += "\"ledPin\":" + String(LED_PIN) + ",";
  json += "\"colorOrder\":\"RGB\",";
  json += "\"ledType\":\"WS2812B\",";
  json += "\"maxLEDs\":" + String(MAX_LEDS) + ",";
  
  // Add individual strip information
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
  json += "]";
  
  json += "}";
  return json;
}

String generatePlaylistJSON() {
  String json = "{\"name\":\"" + playlistName + "\",\"loop\":" + String(playlistLoopEnabled ? "true" : "false") + ",\"items\":[";
  for (size_t i = 0; i < currentPlaylist.size(); i++) {
    if (i > 0) json += ",";
    const auto& item = currentPlaylist[i];
    json += "{\"name\":\"" + item.animationName + "\",\"duration\":" + String(item.duration) + 
           ",\"speed\":" + String(item.speedMultiplier) + ",\"enabled\":" + String(item.enabled ? "true" : "false") + "}";
  }
  json += "]}";
  return json;
}

// =================== SERIAL COMMANDS ===================

void handleSerialCommands() {
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    command.toLowerCase();
    
    if (command == "status") {
      printSystemStatus();
    } else if (command == "play") {
      startStandalonePlayback();
    } else if (command == "stop") {
      stopStandalonePlayback();
    } else if (command == "next") {
      if (isPlayingStandalone) {
        nextPlaylistItem();
      } else {
        Serial.println("Not currently playing");
      }
    } else if (command == "list") {
      listAnimations();
    } else if (command == "storage") {
      printStorageInfo();
    } else if (command == "restart") {
      Serial.println("Restarting ESP32...");
      ESP.restart();
    } else if (command == "createplaylist") {
      createDefaultPlaylist();
      Serial.println("✅ Created default playlist with all animations");
    } else if (command.startsWith("play ")) {
      String animName = command.substring(5);
      playSpecificAnimation(animName);
    } else if (command == "ledtest") {
      testLEDStrip();
    } else if (command == "ledinfo") {
      printLEDInfo();
    } else if (command == "colortest") {
      testColorOrder();
    } else {
      Serial.println("Commands: status, play, stop, next, list, storage, restart, createplaylist");
      Serial.println("          play [animation_name] - Play specific animation");
      Serial.println("          ledtest - Test LED strip with rainbow pattern");
      Serial.println("          ledinfo - Show LED configuration and test basic colors");
    }
  }
}

void printSystemStatus() {
  Serial.println("\n📊 ESP32 ANIMATION PLAYER STATUS");
  Serial.println("=================================");
  Serial.println("🎬 Playback: " + String(isPlayingStandalone ? "Playing" : "Stopped"));
  if (isPlayingStandalone && !currentPlaylist.empty()) {
    Serial.println("🎵 Current: " + currentPlaylist[currentPlaylistIndex].animationName);
  }
  Serial.println("📚 Library: " + String(animationLibrary.size()) + " animations");
  Serial.println("📋 Playlist: " + playlistName + " (" + String(currentPlaylist.size()) + " items)");
  Serial.println("📶 WiFi: " + WiFi.localIP().toString());
  Serial.println("🌐 Web: http://" + WiFi.localIP().toString());
  Serial.println("💾 Free Heap: " + String(ESP.getFreeHeap()) + " bytes");
  Serial.println("⏰ Uptime: " + String(millis() / 1000) + " seconds");
  printStorageInfo();
  Serial.println("=================================");
}

void listAnimations() {
  Serial.println("\n📚 ANIMATION LIBRARY");
  Serial.println("====================");
  for (const auto& anim : animationLibrary) {
    Serial.println("- " + anim.name + " (" + String(anim.duration, 1) + "s, " + 
                  String(anim.fileSize / 1024) + "KB, " + String(anim.frameCount) + " frames)");
  }
  Serial.println("====================");
}

void printStorageInfo() {
  size_t totalBytes = SPIFFS.totalBytes();
  size_t usedBytes = SPIFFS.usedBytes();
  float usagePercent = (float)usedBytes / totalBytes * 100;
  
  Serial.println("📦 Storage: " + String(usedBytes / 1024) + "KB / " + 
                String(totalBytes / 1024) + "KB (" + String(usagePercent, 1) + "%)");
}

void playSpecificAnimation(String animName) {
  animName.trim();
  
  // Find the animation in the library
  StoredAnimation* targetAnim = nullptr;
  for (auto& anim : animationLibrary) {
    if (anim.name.equalsIgnoreCase(animName)) {
      targetAnim = &anim;
      break;
    }
  }
  
  if (!targetAnim) {
    Serial.println("❌ Animation '" + animName + "' not found");
    Serial.println("Available animations:");
    for (const auto& anim : animationLibrary) {
      Serial.println("  - " + anim.name);
    }
    return;
  }
  
  // Create a temporary single-item playlist
  currentPlaylist.clear();
  PlaylistItem item;
  item.animationName = targetAnim->name;
  item.duration = targetAnim->duration;
  item.speedMultiplier = 1.0;
  item.enabled = true;
  currentPlaylist.push_back(item);
  
  playlistName = "Single: " + animName;
  currentPlaylistIndex = 0;
  
  Serial.println("🎬 Playing: " + animName + " (" + String(targetAnim->duration, 1) + "s)");
  
  // Start playback
  startStandalonePlayback();
}

void testLEDStrip() {
  Serial.println("🧪 Testing LED strip...");
  Serial.println("💡 Configuration: " + String(ledConfig.totalLEDs) + " LEDs");
  
  // Stop any current playback
  bool wasPlaying = isPlayingStandalone;
  stopStandalonePlayback();
  
  // Test 1: All red
  Serial.println("🔴 Test 1: All RED");
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Red);
  FastLED.show();
  delay(1000);
  
  // Test 2: All green  
  Serial.println("🟢 Test 2: All GREEN");
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Green);
  FastLED.show();
  delay(1000);
  
  // Test 3: All blue
  Serial.println("🔵 Test 3: All BLUE");
  fill_solid(leds, ledConfig.totalLEDs, CRGB::Blue);
  FastLED.show();
  delay(1000);
  
  // Test 4: Rainbow sweep
  Serial.println("🌈 Test 4: Rainbow sweep");
  for (int hue = 0; hue < 256; hue += 4) {
    for (int i = 0; i < ledConfig.totalLEDs; i++) {
      leds[i] = CHSV(hue + (i * 2), 255, 128);
    }
    FastLED.show();
    delay(20);
  }
  
  // Test 5: Individual LED test (first 10 LEDs)
  Serial.println("🎯 Test 5: Individual LEDs (first 10)");
  FastLED.clear();
  for (int i = 0; i < min(10, ledConfig.totalLEDs); i++) {
    leds[i] = CRGB::White;
    FastLED.show();
    Serial.println("  LED " + String(i) + " ON");
    delay(300);
    leds[i] = CRGB::Black;
    FastLED.show();
    delay(100);
  }
  
  // Clear all
  FastLED.clear();
  FastLED.show();
  Serial.println("✅ LED test complete");
  
  // Restart playback if it was running
  if (wasPlaying) {
    delay(1000);
    startStandalonePlayback();
  }
}

void printLEDInfo() {
  Serial.println("\n💡 LED STRIP INFORMATION");
  Serial.println("========================");
  Serial.println("🔧 Total LEDs: " + String(ledConfig.totalLEDs));
  Serial.println("📊 Segments: " + String(ledConfig.numSegments));
  Serial.println("📏 LEDs per segment: " + String(ledConfig.ledsPerSegment));
  Serial.println("⚙️ Configured: " + String(ledConfig.isConfigured ? "Yes" : "No"));
  Serial.println("📡 Config source: " + ledConfig.lastConfigSource);
  Serial.println("🎚️ Current brightness: " + String(currentBrightness));
  Serial.println("🔌 LED pin: GPIO " + String(LED_PIN));
  Serial.println("🎨 Color order: RGB");
  Serial.println("📺 LED type: WS2812B");
  
  // Show individual strip information if configured
  if (ledConfig.isConfigured && ledConfig.numSegments > 1) {
    Serial.println("📋 Individual Strip Mapping:");
    for (int i = 0; i < ledConfig.numSegments && i < 10; i++) {
      Serial.println("   Strip " + String(i) + ": " + String(ledConfig.stripLEDs[i]) + 
                    " LEDs (indices " + String(ledConfig.stripStartIndices[i]) + 
                    "-" + String(ledConfig.stripStartIndices[i] + ledConfig.stripLEDs[i] - 1) + ")");
    }
  }
  
  Serial.println("========================");
  
  // Quick 3-color test
  Serial.println("🧪 Quick color test...");
  fill_solid(leds, min(5, ledConfig.totalLEDs), CRGB::Red);
  FastLED.show();
  delay(500);
  fill_solid(leds, min(5, ledConfig.totalLEDs), CRGB::Green);
  FastLED.show();
  delay(500);
  fill_solid(leds, min(5, ledConfig.totalLEDs), CRGB::Blue);
  FastLED.show();
  delay(500);
  FastLED.clear();
  FastLED.show();
}

void testColorOrder() {
  Serial.println("🎨 Testing Color Order...");
  Serial.println("Watch the first 10 LEDs - they should show:");
  Serial.println("RED, GREEN, BLUE sequence");
  
  // Stop any current playback
  bool wasPlaying = isPlayingStandalone;
  stopStandalonePlayback();
  
  FastLED.clear();
  
  // Test pure colors on first 10 LEDs
  for (int i = 0; i < min(10, ledConfig.totalLEDs); i += 3) {
    if (i < ledConfig.totalLEDs) leds[i] = CRGB(255, 0, 0);     // Should be RED
    if (i+1 < ledConfig.totalLEDs) leds[i+1] = CRGB(0, 255, 0); // Should be GREEN  
    if (i+2 < ledConfig.totalLEDs) leds[i+2] = CRGB(0, 0, 255); // Should be BLUE
  }
  
  FastLED.show();
  Serial.println("🔴 If RED LEDs look GREEN, change COLOR_ORDER to GRB");
  Serial.println("🟢 If GREEN LEDs look BLUE, change COLOR_ORDER to BRG"); 
  Serial.println("🔵 If BLUE LEDs look RED, change COLOR_ORDER to BGR");
  Serial.println("✅ If colors are correct, COLOR_ORDER is right");
  
  delay(5000); // Show for 5 seconds
  
  FastLED.clear();
  FastLED.show();
  
  // Restart playback if it was running
  if (wasPlaying) {
    delay(1000);
    startStandalonePlayback();
  }
}
