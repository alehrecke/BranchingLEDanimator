/*
 * ESP32 Audio + SD Card Test Script for Branching LED Animator
 * Hardware Setup Validation and Audio Capability Testing
 * 
 * This script tests the new audio hardware components before integration
 * with the existing LED animation system.
 * 
 * Hardware Requirements:
 * - ESP32 development board
 * - SD Card Reader Module:
 *   - MISO → GPIO 19
 *   - MOSI → GPIO 23  
 *   - SCK → GPIO 18
 *   - CS → GPIO 5
 * - I2S Stereo DAC Module (Adafruit):
 *   - DIN → GPIO 22
 *   - BCLK → GPIO 26
 *   - WSEL → GPIO 25
 * - Both modules connected to 3.3V and GND
 * - Speakers connected to I2S DAC output
 * 
 * Features Tested:
 * - SD card mounting and file operations
 * - I2S audio output with test tones
 * - WAV file playback from SD card
 * - Serial command interface for testing
 * - Basic LED feedback during audio operations
 */

#include <WiFi.h>
#include <SD.h>
#include <SPI.h>
#include <driver/i2s.h>
#include <FastLED.h>

// ===================== HARDWARE CONFIGURATION =====================
// SD Card Module Pins
#define SD_MISO_PIN 19
#define SD_MOSI_PIN 23
#define SD_SCK_PIN  18
#define SD_CS_PIN   5

// I2S DAC Module Pins
#define I2S_DIN_PIN   22  // Data
#define I2S_BCLK_PIN  26  // Bit Clock
#define I2S_WSEL_PIN  25  // Word Select (L/R Clock)

// LED Feedback (moved to GPIO 2 to resolve SD card conflict)
#define LED_PIN 2  // Moved from GPIO 19 to avoid SD card MISO conflict
#define NUM_LEDS 10 // Just a few LEDs for status feedback
#define LED_TYPE WS2812B
#define COLOR_ORDER RGB

// I2S Configuration
#define I2S_PORT I2S_NUM_0
#define I2S_SAMPLE_RATE 44100
#define I2S_BITS_PER_SAMPLE 16
#define I2S_CHANNELS 2
#define I2S_BUFFER_COUNT 8
#define I2S_BUFFER_SIZE 1024

// ==================== GLOBAL VARIABLES ====================
CRGB leds[NUM_LEDS];
bool sdCardMounted = false;
bool i2sInitialized = false;
bool audioPlaying = false;

// Audio buffer for I2S output
int16_t audioBuffer[I2S_BUFFER_SIZE * 2]; // Stereo buffer

// Test tone generation
float toneFrequency = 440.0; // A4 note
float tonePhase = 0.0;
// Note: TWO_PI is already defined in Arduino.h, so we'll use that

// ======================= SETUP =======================
void setup() {
  Serial.begin(115200);
  delay(1000);
  
  Serial.println("\n🎵 ESP32 Audio + SD Card Test Starting...");
  Serial.println("Hardware: SD Card Reader + I2S Stereo DAC");
  Serial.println("Purpose: Validate audio hardware before LED integration");
  
  // Initialize LED feedback system (minimal setup)
  setupLEDFeedback();
  
  // Test SD Card functionality
  setupSDCard();
  
  // Test I2S Audio functionality
  setupI2S();
  
  // Show startup completion
  showStartupPattern();
  
  Serial.println("\n✅ ESP32 Audio Test Ready!");
  Serial.println("📋 Available Commands:");
  Serial.println("   'sd' - Test SD card operations");
  Serial.println("   'tone' - Play test tone");
  Serial.println("   'wav' - Play WAV file from SD");
  Serial.println("   'stop' - Stop audio playback");
  Serial.println("   'status' - Show system status");
  Serial.println("   'help' - Show this help");
}

// ======================= MAIN LOOP =======================
void loop() {
  // Handle serial commands
  handleSerialCommands();
  
  // Update audio playback if active
  if (audioPlaying) {
    updateAudioPlayback();
  }
  
  // Update LED feedback
  updateLEDFeedback();
  
  delay(10); // Small delay to prevent watchdog issues
}

// ===================== SETUP FUNCTIONS =====================

void setupLEDFeedback() {
  Serial.println("💡 Initializing LED feedback system...");
  
  // LED_PIN moved to GPIO 2 to resolve SD card conflict
  Serial.println("✅ LED pin moved to GPIO 2 (resolved SD card conflict)");
  
  FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, NUM_LEDS);
  FastLED.setBrightness(64);
  FastLED.clear();
  FastLED.show();
  
  Serial.println("✅ LED feedback system initialized");
}

void setupSDCard() {
  Serial.println("📁 Initializing SD Card...");
  
  // Add delay to ensure SD card is ready
  delay(100);
  
  Serial.println("🔧 Configuring SPI for SD card...");
  Serial.printf("   MISO: GPIO %d\n", SD_MISO_PIN);
  Serial.printf("   MOSI: GPIO %d\n", SD_MOSI_PIN);
  Serial.printf("   SCK:  GPIO %d\n", SD_SCK_PIN);
  Serial.printf("   CS:   GPIO %d\n", SD_CS_PIN);
  
  // Configure SPI for SD card with explicit pins
  SPI.begin(SD_SCK_PIN, SD_MISO_PIN, SD_MOSI_PIN, SD_CS_PIN);
  
  // Try different SD card initialization methods
  Serial.println("🔄 Attempting SD card mount (method 1)...");
  if (SD.begin(SD_CS_PIN)) {
    sdCardMounted = true;
    Serial.println("✅ SD Card mounted successfully (method 1)");
  } else {
    Serial.println("❌ Method 1 failed, trying method 2...");
    
    // Try with explicit SPI settings
    if (SD.begin(SD_CS_PIN, SPI, 4000000)) { // 4MHz SPI speed
      sdCardMounted = true;
      Serial.println("✅ SD Card mounted successfully (method 2 - slower speed)");
    } else {
      Serial.println("❌ Method 2 failed, trying method 3...");
      
      // Try even slower speed
      if (SD.begin(SD_CS_PIN, SPI, 1000000)) { // 1MHz SPI speed
        sdCardMounted = true;
        Serial.println("✅ SD Card mounted successfully (method 3 - very slow speed)");
      } else {
        Serial.println("❌ All SD card mount methods failed!");
        Serial.println("🔍 Troubleshooting checklist:");
        Serial.println("   1. Check all wiring connections:");
        Serial.println("      MISO → GPIO 19");
        Serial.println("      MOSI → GPIO 23");
        Serial.println("      SCK  → GPIO 18");
        Serial.println("      CS   → GPIO 5");
        Serial.println("      VCC  → 3.3V");
        Serial.println("      GND  → GND");
        Serial.println("   2. Ensure SD card is properly inserted");
        Serial.println("   3. Try a different SD card (FAT32 formatted)");
        Serial.println("   4. Check SD card module compatibility");
        Serial.println("   5. Verify power supply can handle both ESP32 and SD card");
        return;
      }
    }
  }
  
  if (sdCardMounted) {
    // Get SD card info
    uint64_t cardSize = SD.cardSize() / (1024 * 1024);
    Serial.printf("📊 SD Card Size: %lluMB\n", cardSize);
    
    uint8_t cardType = SD.cardType();
    Serial.print("📋 SD Card Type: ");
    switch(cardType) {
      case CARD_MMC:
        Serial.println("MMC");
        break;
      case CARD_SD:
        Serial.println("SDSC");
        break;
      case CARD_SDHC:
        Serial.println("SDHC");
        break;
      default:
        Serial.println("UNKNOWN");
        break;
    }
    
    // Test basic file operations
    testSDCardOperations();
  }
}

void setupI2S() {
  Serial.println("🎵 Initializing I2S Audio...");
  
  // I2S configuration (updated for newer ESP32 Arduino Core)
  i2s_config_t i2s_config = {
    .mode = (i2s_mode_t)(I2S_MODE_MASTER | I2S_MODE_TX),
    .sample_rate = I2S_SAMPLE_RATE,
    .bits_per_sample = (i2s_bits_per_sample_t)I2S_BITS_PER_SAMPLE,
    .channel_format = I2S_CHANNEL_FMT_RIGHT_LEFT,
    .communication_format = I2S_COMM_FORMAT_STAND_I2S, // Updated from deprecated I2S_COMM_FORMAT_I2S
    .intr_alloc_flags = ESP_INTR_FLAG_LEVEL1,
    .dma_buf_count = I2S_BUFFER_COUNT,
    .dma_buf_len = I2S_BUFFER_SIZE,
    .use_apll = false,
    .tx_desc_auto_clear = true
  };
  
  // I2S pin configuration
  i2s_pin_config_t pin_config = {
    .bck_io_num = I2S_BCLK_PIN,
    .ws_io_num = I2S_WSEL_PIN,
    .data_out_num = I2S_DIN_PIN,
    .data_in_num = I2S_PIN_NO_CHANGE
  };
  
  // Install and start I2S driver
  esp_err_t result = i2s_driver_install(I2S_PORT, &i2s_config, 0, NULL);
  if (result != ESP_OK) {
    Serial.printf("❌ I2S driver install failed: %s\n", esp_err_to_name(result));
    return;
  }
  
  result = i2s_set_pin(I2S_PORT, &pin_config);
  if (result != ESP_OK) {
    Serial.printf("❌ I2S pin config failed: %s\n", esp_err_to_name(result));
    return;
  }
  
  // Start I2S
  result = i2s_start(I2S_PORT);
  if (result != ESP_OK) {
    Serial.printf("❌ I2S start failed: %s\n", esp_err_to_name(result));
    return;
  }
  
  i2sInitialized = true;
  Serial.println("✅ I2S Audio initialized successfully");
  Serial.printf("🎵 Sample Rate: %d Hz, %d-bit, %d channels\n", 
                I2S_SAMPLE_RATE, I2S_BITS_PER_SAMPLE, I2S_CHANNELS);
  Serial.println("🔍 Check connections:");
  Serial.println("   DIN  → GPIO 22");
  Serial.println("   BCLK → GPIO 26");
  Serial.println("   WSEL → GPIO 25");
  Serial.println("   Speakers connected to DAC output");
}

void showStartupPattern() {
  Serial.println("🎆 Startup pattern...");
  
  // Show status via serial and LEDs
  Serial.println("✅ SD Card: " + String(sdCardMounted ? "Ready" : "Failed"));
  Serial.println("✅ I2S Audio: " + String(i2sInitialized ? "Ready" : "Failed"));
  
  // LED status indication
  FastLED.clear();
  if (sdCardMounted) {
    leds[0] = CRGB::Green;  // Green = SD card OK
  } else {
    leds[0] = CRGB::Red;    // Red = SD card failed
  }
  
  if (i2sInitialized) {
    leds[1] = CRGB::Blue;   // Blue = I2S OK
  } else {
    leds[1] = CRGB::Red;    // Red = I2S failed
  }
  
  FastLED.show();
  delay(2000); // Show status for 2 seconds
  
  if (sdCardMounted && i2sInitialized) {
    Serial.println("🎉 All systems ready for audio testing!");
    // Success pattern - rainbow sweep
    for (int i = 0; i < NUM_LEDS; i++) {
      leds[i] = CHSV(i * 25, 255, 128);
    }
    FastLED.show();
    delay(1000);
    FastLED.clear();
    FastLED.show();
  } else {
    Serial.println("⚠️ Some systems failed - check hardware connections");
    // Error pattern - red flash
    for (int i = 0; i < 3; i++) {
      fill_solid(leds, NUM_LEDS, CRGB::Red);
      FastLED.show();
      delay(200);
      FastLED.clear();
      FastLED.show();
      delay(200);
    }
  }
}

// =================== SD CARD FUNCTIONS ===================

void testSDCardOperations() {
  Serial.println("🧪 Testing SD card operations...");
  
  // Check if SD card is still mounted
  if (!sdCardMounted) {
    Serial.println("❌ SD card not mounted - attempting remount...");
    setupSDCard();
    if (!sdCardMounted) {
      Serial.println("❌ SD card remount failed");
      return;
    }
  }
  
  // Add delay to ensure SD card is ready
  delay(100);
  
  // Test 1: Create a test file
  Serial.println("📝 Creating test file...");
  File testFile = SD.open("/test_audio_system.txt", FILE_WRITE);
  if (testFile) {
    testFile.println("ESP32 Audio System Test");
    testFile.println("Timestamp: " + String(millis()));
    testFile.println("SD Card: Working");
    testFile.flush(); // Ensure data is written
    testFile.close();
    Serial.println("✅ Test file created successfully");
  } else {
    Serial.println("❌ Failed to create test file");
    Serial.println("🔍 Possible causes:");
    Serial.println("   - SD card write-protected");
    Serial.println("   - Filesystem corruption");
    Serial.println("   - Insufficient space");
    Serial.println("   - Card needs reformatting");
  }
  
  // Test 2: Read the test file
  testFile = SD.open("/test_audio_system.txt", FILE_READ);
  if (testFile) {
    Serial.println("📄 Test file contents:");
    while (testFile.available()) {
      Serial.println("   " + testFile.readString());
    }
    testFile.close();
    Serial.println("✅ Test file read successfully");
  } else {
    Serial.println("❌ Failed to read test file");
  }
  
  // Test 3: List files on SD card
  listSDCardFiles();
  
  // Test 4: Create audio directory
  if (!SD.exists("/audio")) {
    if (SD.mkdir("/audio")) {
      Serial.println("✅ Created /audio directory");
    } else {
      Serial.println("❌ Failed to create /audio directory");
    }
  } else {
    Serial.println("📁 /audio directory already exists");
  }
}

void listSDCardFiles() {
  Serial.println("📂 SD Card file listing:");
  
  // Try to open root directory
  File root = SD.open("/");
  if (!root) {
    Serial.println("❌ Failed to open root directory");
    Serial.println("🔄 Attempting to remount SD card...");
    
    // Try to remount
    SD.end();
    delay(100);
    if (SD.begin(SD_CS_PIN)) {
      Serial.println("✅ SD card remounted successfully");
      root = SD.open("/");
    } else {
      Serial.println("❌ SD card remount failed");
      return;
    }
  }
  
  if (!root) {
    Serial.println("❌ Still cannot open root directory");
    return;
  }
  
  Serial.println("📁 Root directory contents:");
  int fileCount = 0;
  File file = root.openNextFile();
  while (file) {
    fileCount++;
    if (file.isDirectory()) {
      Serial.println("  📁 " + String(file.name()) + "/");
    } else {
      Serial.printf("  📄 %s (%d bytes)\n", file.name(), file.size());
    }
    file.close();
    file = root.openNextFile();
  }
  root.close();
  
  if (fileCount == 0) {
    Serial.println("  (Empty directory)");
  } else {
    Serial.printf("📊 Total items: %d\n", fileCount);
  }
}

void sdCardDiagnostics() {
  Serial.println("\n🔍 SD CARD DIAGNOSTICS");
  Serial.println("======================");
  
  // Check SD card mount status
  Serial.println("📊 Mount Status: " + String(sdCardMounted ? "✅ Mounted" : "❌ Not Mounted"));
  
  if (!sdCardMounted) {
    Serial.println("🔄 Attempting to remount SD card...");
    setupSDCard();
    return;
  }
  
  // Card information
  uint64_t cardSize = SD.cardSize() / (1024 * 1024);
  uint64_t usedSize = SD.usedBytes() / (1024 * 1024);
  uint8_t cardType = SD.cardType();
  
  Serial.printf("📋 Card Size: %lluMB\n", cardSize);
  Serial.printf("💾 Used Space: %lluMB\n", usedSize);
  Serial.printf("🆓 Free Space: %lluMB\n", cardSize - usedSize);
  
  Serial.print("📋 Card Type: ");
  switch(cardType) {
    case CARD_MMC:
      Serial.println("MMC");
      break;
    case CARD_SD:
      Serial.println("SDSC (Standard Capacity)");
      break;
    case CARD_SDHC:
      Serial.println("SDHC (High Capacity)");
      break;
    default:
      Serial.println("UNKNOWN");
      break;
  }
  
  // Test basic operations
  Serial.println("\n🧪 Testing Basic Operations:");
  
  // Test 1: Write test
  Serial.println("1. Write Test...");
  File testFile = SD.open("/diagnostic_test.txt", FILE_WRITE);
  if (testFile) {
    testFile.println("SD Card Diagnostic Test");
    testFile.println("Timestamp: " + String(millis()));
    testFile.close();
    Serial.println("   ✅ Write test passed");
  } else {
    Serial.println("   ❌ Write test failed");
  }
  
  // Test 2: Read test
  Serial.println("2. Read Test...");
  testFile = SD.open("/diagnostic_test.txt", FILE_READ);
  if (testFile) {
    String content = testFile.readString();
    testFile.close();
    Serial.println("   ✅ Read test passed");
    Serial.println("   📄 Content: " + content.substring(0, 50) + "...");
  } else {
    Serial.println("   ❌ Read test failed");
  }
  
  // Test 3: Directory operations
  Serial.println("3. Directory Test...");
  if (SD.mkdir("/test_dir")) {
    Serial.println("   ✅ Directory creation passed");
    if (SD.rmdir("/test_dir")) {
      Serial.println("   ✅ Directory removal passed");
    } else {
      Serial.println("   ⚠️ Directory removal failed");
    }
  } else {
    Serial.println("   ❌ Directory creation failed");
  }
  
  // Test 4: List files
  Serial.println("4. File Listing Test...");
  listSDCardFiles();
  
  Serial.println("======================");
  Serial.println("💡 If tests fail, check:");
  Serial.println("   - SD card is FAT32 formatted");
  Serial.println("   - Card is properly inserted");
  Serial.println("   - Wiring connections are secure");
  Serial.println("   - Power supply is adequate");
}

void testSDCardHardware() {
  Serial.println("\n🔧 SD CARD HARDWARE TEST");
  Serial.println("========================");
  Serial.println("Testing SD card module wiring WITHOUT card inserted");
  
  // Test 1: Check if we can communicate with SPI bus
  Serial.println("\n1. SPI Bus Communication Test:");
  Serial.printf("   MISO: GPIO %d\n", SD_MISO_PIN);
  Serial.printf("   MOSI: GPIO %d\n", SD_MOSI_PIN);
  Serial.printf("   SCK:  GPIO %d\n", SD_SCK_PIN);
  Serial.printf("   CS:   GPIO %d\n", SD_CS_PIN);
  
  // Initialize SPI
  SPI.begin(SD_SCK_PIN, SD_MISO_PIN, SD_MOSI_PIN, SD_CS_PIN);
  
  // Test 2: Check CS pin control
  Serial.println("\n2. Chip Select (CS) Pin Test:");
  pinMode(SD_CS_PIN, OUTPUT);
  
  // Test CS pin high/low
  digitalWrite(SD_CS_PIN, HIGH);
  delay(10);
  bool csHigh = digitalRead(SD_CS_PIN);
  
  digitalWrite(SD_CS_PIN, LOW);
  delay(10);
  bool csLow = digitalRead(SD_CS_PIN);
  
  digitalWrite(SD_CS_PIN, HIGH); // Return to default state
  
  Serial.println("   CS HIGH: " + String(csHigh ? "✅ OK" : "❌ FAILED"));
  Serial.println("   CS LOW:  " + String(csLow == 0 ? "✅ OK" : "❌ FAILED"));
  
  // Test 3: SPI Clock Test
  Serial.println("\n3. SPI Clock Test:");
  Serial.println("   Generating test clock pulses on SCK pin...");
  
  pinMode(SD_SCK_PIN, OUTPUT);
  for (int i = 0; i < 10; i++) {
    digitalWrite(SD_SCK_PIN, HIGH);
    delayMicroseconds(1);
    digitalWrite(SD_SCK_PIN, LOW);
    delayMicroseconds(1);
  }
  Serial.println("   ✅ Clock pulses generated (check with oscilloscope/logic analyzer)");
  
  // Test 4: MOSI Test
  Serial.println("\n4. MOSI (Data Out) Test:");
  pinMode(SD_MOSI_PIN, OUTPUT);
  
  digitalWrite(SD_MOSI_PIN, HIGH);
  delay(10);
  bool mosiHigh = digitalRead(SD_MOSI_PIN);
  
  digitalWrite(SD_MOSI_PIN, LOW);
  delay(10);
  bool mosiLow = digitalRead(SD_MOSI_PIN);
  
  Serial.println("   MOSI HIGH: " + String(mosiHigh ? "✅ OK" : "❌ FAILED"));
  Serial.println("   MOSI LOW:  " + String(mosiLow == 0 ? "✅ OK" : "❌ FAILED"));
  
  // Test 5: MISO Test (trickier without card)
  Serial.println("\n5. MISO (Data In) Test:");
  pinMode(SD_MISO_PIN, INPUT_PULLUP);
  delay(10);
  bool misoState = digitalRead(SD_MISO_PIN);
  
  Serial.println("   MISO State: " + String(misoState ? "HIGH (pullup working)" : "LOW"));
  Serial.println("   💡 Without SD card, MISO should be HIGH due to pullup");
  Serial.println("   💡 If LOW, check wiring or try external pullup resistor");
  
  // Test 6: Power Test
  Serial.println("\n6. Power Supply Test:");
  Serial.println("   💡 Check SD module power LED (if present):");
  Serial.println("   ✅ LED ON  = Module getting power");
  Serial.println("   ❌ LED OFF = Power wiring issue");
  Serial.println("   ⚠️  No LED = Some modules don't have power LEDs");
  
  // Test 7: Voltage levels (if possible)
  Serial.println("\n7. Voltage Check:");
  Serial.println("   💡 Use multimeter to verify:");
  Serial.println("   📍 VCC pin: Should read 3.3V");
  Serial.println("   📍 GND pin: Should read 0V");
  Serial.println("   📍 CS pin:  Should read 3.3V (when idle)");
  
  // Test 8: Try basic SPI communication
  Serial.println("\n8. SPI Communication Test:");
  Serial.println("   Attempting basic SPI transaction...");
  
  SPI.beginTransaction(SPISettings(1000000, MSBFIRST, SPI_MODE0));
  digitalWrite(SD_CS_PIN, LOW);
  
  // Send CMD0 (GO_IDLE_STATE) - this is the first command SD cards expect
  uint8_t response = SPI.transfer(0x40); // CMD0 command
  SPI.transfer(0x00); // Argument
  SPI.transfer(0x00);
  SPI.transfer(0x00);
  SPI.transfer(0x00);
  SPI.transfer(0x95); // CRC for CMD0
  
  // Try to read response
  for (int i = 0; i < 8; i++) {
    response = SPI.transfer(0xFF);
    if (response != 0xFF) break;
  }
  
  digitalWrite(SD_CS_PIN, HIGH);
  SPI.endTransaction();
  
  Serial.printf("   SPI Response: 0x%02X\n", response);
  if (response == 0xFF) {
    Serial.println("   ❌ No response (likely wiring issue or no card)");
  } else {
    Serial.println("   ✅ Got response (wiring probably OK, but no card detected)");
  }
  
  // Summary
  Serial.println("\n========================");
  Serial.println("🔍 HARDWARE TEST SUMMARY:");
  Serial.println("========================");
  Serial.println("✅ If all pin tests pass: Wiring is likely correct");
  Serial.println("❌ If pin tests fail: Check connections and power");
  Serial.println("💡 SPI response 0xFF: Normal without SD card");
  Serial.println("💡 Power LED (if present): Should be ON");
  Serial.println("");
  Serial.println("🎯 Next steps:");
  Serial.println("   1. Insert properly formatted SD card (FAT32)");
  Serial.println("   2. Run 'sdinfo' command to test with card");
  Serial.println("   3. If still fails, try different SD card");
}

void testPowerSupply() {
  Serial.println("\n⚡ POWER SUPPLY DIAGNOSTICS");
  Serial.println("==========================");
  
  // Test ESP32 internal voltages (approximate)
  Serial.println("🔋 ESP32 Power Analysis:");
  Serial.printf("   Free Heap: %d bytes\n", ESP.getFreeHeap());
  Serial.printf("   CPU Frequency: %d MHz\n", getCpuFrequencyMhz());
  
  // Test GPIO voltage levels
  Serial.println("\n📊 GPIO Voltage Test:");
  Serial.println("   Testing 3.3V output capability...");
  
  // Set all SD pins as outputs and drive HIGH to test voltage under load
  pinMode(SD_CS_PIN, OUTPUT);
  pinMode(SD_MOSI_PIN, OUTPUT);
  pinMode(SD_SCK_PIN, OUTPUT);
  
  digitalWrite(SD_CS_PIN, HIGH);
  digitalWrite(SD_MOSI_PIN, HIGH);
  digitalWrite(SD_SCK_PIN, HIGH);
  
  delay(100); // Let voltages stabilize
  
  // Read back the voltages (should be HIGH if power is good)
  bool csVoltage = digitalRead(SD_CS_PIN);
  bool mosiVoltage = digitalRead(SD_MOSI_PIN);
  bool sckVoltage = digitalRead(SD_SCK_PIN);
  
  Serial.println("   CS Pin (GPIO 5):   " + String(csVoltage ? "HIGH (~3.3V)" : "LOW (power issue?)"));
  Serial.println("   MOSI Pin (GPIO 23): " + String(mosiVoltage ? "HIGH (~3.3V)" : "LOW (power issue?)"));
  Serial.println("   SCK Pin (GPIO 18):  " + String(sckVoltage ? "HIGH (~3.3V)" : "LOW (power issue?)"));
  
  // Test MISO with pullup
  pinMode(SD_MISO_PIN, INPUT_PULLUP);
  delay(10);
  bool misoVoltage = digitalRead(SD_MISO_PIN);
  Serial.println("   MISO Pin (GPIO 19): " + String(misoVoltage ? "HIGH (pullup working)" : "LOW (power issue?)"));
  
  // Power consumption test
  Serial.println("\n🔌 Power Consumption Analysis:");
  Serial.println("   ESP32 alone: ~80-240mA (depending on WiFi usage)");
  Serial.println("   SD Card Module: ~50-200mA (during read/write)");
  Serial.println("   I2S DAC: ~20-50mA");
  Serial.println("   LED Strip: Varies by LED count and brightness");
  
  Serial.println("\n💡 POWER TROUBLESHOOTING:");
  Serial.println("========================");
  
  // Check common power issues
  Serial.println("🔍 Common Power Issues:");
  Serial.println("   1. USB Power Limitation:");
  Serial.println("      • USB 2.0: Max 500mA");
  Serial.println("      • USB 3.0: Max 900mA");
  Serial.println("      • Some USB ports: Only 100mA");
  Serial.println("      ➡️ Try powered USB hub or external 5V supply");
  
  Serial.println("\n   2. Breadboard Issues:");
  Serial.println("      • Loose connections cause voltage drops");
  Serial.println("      • Long wires increase resistance");
  Serial.println("      • Multiple devices sharing power rails");
  Serial.println("      ➡️ Check all connections, use short wires");
  
  Serial.println("\n   3. ESP32 3.3V Regulator:");
  Serial.println("      • Limited to ~600mA total");
  Serial.println("      • Can drop voltage under heavy load");
  Serial.println("      • Gets hot with high current draw");
  Serial.println("      ➡️ Use external 3.3V regulator if needed");
  
  Serial.println("\n   4. SD Card Module Variations:");
  Serial.println("      • Some modules need 5V input (with onboard regulator)");
  Serial.println("      • Some are 3.3V only");
  Serial.println("      • Check module specifications");
  Serial.println("      ➡️ Try connecting VCC to 5V instead of 3.3V");
  
  Serial.println("\n🎯 IMMEDIATE FIXES TO TRY:");
  Serial.println("==========================");
  Serial.println("1. 🔌 Try External Power:");
  Serial.println("   • Use 5V wall adapter (2A+) instead of USB");
  Serial.println("   • Or powered USB hub");
  
  Serial.println("\n2. 🔧 Try 5V for SD Module:");
  Serial.println("   • Disconnect SD module VCC from 3.3V");
  Serial.println("   • Connect SD module VCC to ESP32 VIN (5V)");
  Serial.println("   • Many SD modules have onboard 3.3V regulators");
  
  Serial.println("\n3. 🔗 Check Connections:");
  Serial.println("   • Re-seat all breadboard connections");
  Serial.println("   • Use shorter, thicker wires");
  Serial.println("   • Ensure good ground connection");
  
  Serial.println("\n4. 📏 Measure with Multimeter:");
  Serial.println("   • ESP32 3.3V pin: Should be 3.2-3.4V");
  Serial.println("   • ESP32 VIN pin: Should be ~5V (when USB powered)");
  Serial.println("   • SD module VCC pin: Should match supply voltage");
  Serial.println("   • All GND pins: Should be 0V");
  
  Serial.println("\n⚠️ WARNING SIGNS:");
  Serial.println("   • ESP32 getting warm/hot = overcurrent");
  Serial.println("   • Voltage dropping below 3.0V = insufficient power");
  Serial.println("   • Intermittent operation = loose connections");
  
  Serial.println("\n========================");
}

void fixSDCardIssues() {
  Serial.println("\n🔧 SD CARD REPAIR ATTEMPT");
  Serial.println("==========================");
  
  Serial.println("🔄 Step 1: Unmounting SD card...");
  SD.end();
  delay(500);
  
  Serial.println("🔄 Step 2: Re-initializing SPI...");
  SPI.end();
  delay(100);
  SPI.begin(SD_SCK_PIN, SD_MISO_PIN, SD_MOSI_PIN, SD_CS_PIN);
  delay(100);
  
  Serial.println("🔄 Step 3: Attempting remount with different speeds...");
  
  // Try multiple mount methods
  bool mounted = false;
  
  // Method 1: Standard speed
  if (SD.begin(SD_CS_PIN)) {
    Serial.println("✅ Mounted at standard speed");
    mounted = true;
  }
  // Method 2: Slower speed  
  else if (SD.begin(SD_CS_PIN, SPI, 4000000)) {
    Serial.println("✅ Mounted at 4MHz (slower speed)");
    mounted = true;
  }
  // Method 3: Very slow speed
  else if (SD.begin(SD_CS_PIN, SPI, 1000000)) {
    Serial.println("✅ Mounted at 1MHz (very slow speed)");
    mounted = true;
  }
  
  if (mounted) {
    sdCardMounted = true;
    Serial.println("🎉 SD card successfully remounted!");
    
    // Test basic operations
    Serial.println("🧪 Testing basic operations...");
    testSDCardOperations();
  } else {
    Serial.println("❌ All remount attempts failed");
    Serial.println("💡 Try these manual fixes:");
    Serial.println("   1. Remove and reinsert SD card");
    Serial.println("   2. Format SD card as FAT32 on computer");
    Serial.println("   3. Try a different SD card");
    Serial.println("   4. Check for write-protection switch");
    sdCardMounted = false;
  }
  
  Serial.println("==========================");
}

// =================== I2S AUDIO FUNCTIONS ===================

void generateTestTone(float frequency, float duration) {
  if (!i2sInitialized) {
    Serial.println("❌ I2S not initialized");
    return;
  }
  
  Serial.printf("🎵 Generating test tone: %.1f Hz for %.1f seconds\n", frequency, duration);
  
  audioPlaying = true;
  toneFrequency = frequency;
  tonePhase = 0.0;
  
  unsigned long startTime = millis();
  unsigned long durationMs = (unsigned long)(duration * 1000);
  
  while (millis() - startTime < durationMs && audioPlaying) {
    generateToneBuffer();
    
    size_t bytesWritten;
    esp_err_t result = i2s_write(I2S_PORT, audioBuffer, sizeof(audioBuffer), &bytesWritten, portMAX_DELAY);
    
    if (result != ESP_OK) {
      Serial.printf("❌ I2S write error: %s\n", esp_err_to_name(result));
      break;
    }
    
    // Small delay to prevent watchdog issues
    delay(1);
  }
  
  audioPlaying = false;
  Serial.println("✅ Test tone playback complete");
}

void generateToneBuffer() {
  float phaseIncrement = TWO_PI * toneFrequency / I2S_SAMPLE_RATE;
  
  for (int i = 0; i < I2S_BUFFER_SIZE; i++) {
    // Generate sine wave sample
    float sample = sin(tonePhase) * 0.3; // 30% volume
    int16_t sampleInt = (int16_t)(sample * 32767);
    
    // Stereo output (same signal to both channels)
    audioBuffer[i * 2] = sampleInt;     // Left channel
    audioBuffer[i * 2 + 1] = sampleInt; // Right channel
    
    tonePhase += phaseIncrement;
    if (tonePhase >= TWO_PI) {
      tonePhase -= TWO_PI;
    }
  }
}

void playWAVFile(const char* filename) {
  if (!sdCardMounted) {
    Serial.println("❌ SD card not mounted");
    return;
  }
  
  if (!i2sInitialized) {
    Serial.println("❌ I2S not initialized");
    return;
  }
  
  Serial.printf("🎵 Attempting to play WAV file: %s\n", filename);
  
  File audioFile = SD.open(filename, FILE_READ);
  if (!audioFile) {
    Serial.printf("❌ Failed to open audio file: %s\n", filename);
    Serial.println("💡 To test WAV playback:");
    Serial.println("   1. Copy a 44.1kHz, 16-bit stereo WAV file to SD card");
    Serial.println("   2. Name it 'test.wav' and place in root directory");
    Serial.println("   3. Run command: wav");
    return;
  }
  
  // Simple WAV header parsing (basic implementation)
  audioFile.seek(44); // Skip WAV header (assuming standard 44-byte header)
  
  Serial.println("🎵 Playing WAV file...");
  audioPlaying = true;
  
  size_t bytesRead;
  while (audioFile.available() && audioPlaying) {
    bytesRead = audioFile.read((uint8_t*)audioBuffer, sizeof(audioBuffer));
    
    if (bytesRead > 0) {
      size_t bytesWritten;
      esp_err_t result = i2s_write(I2S_PORT, audioBuffer, bytesRead, &bytesWritten, portMAX_DELAY);
      
      if (result != ESP_OK) {
        Serial.printf("❌ I2S write error: %s\n", esp_err_to_name(result));
        break;
      }
    }
    
    // Allow for stopping playback
    if (Serial.available()) {
      String command = Serial.readStringUntil('\n');
      if (command.indexOf("stop") >= 0) {
        audioPlaying = false;
      }
    }
    
    delay(1); // Prevent watchdog issues
  }
  
  audioFile.close();
  audioPlaying = false;
  Serial.println("✅ WAV file playback complete");
}

void stopAudioPlayback() {
  audioPlaying = false;
  
  if (i2sInitialized) {
    // Clear I2S buffer with silence
    memset(audioBuffer, 0, sizeof(audioBuffer));
    size_t bytesWritten;
    i2s_write(I2S_PORT, audioBuffer, sizeof(audioBuffer), &bytesWritten, 100);
  }
  
  Serial.println("⏹️ Audio playback stopped");
}

// =================== UPDATE FUNCTIONS ===================

void updateAudioPlayback() {
  // This function would handle continuous audio playback
  // For now, it's handled within the specific playback functions
}

void updateLEDFeedback() {
  // LED feedback for audio status
  static unsigned long lastUpdate = 0;
  static bool ledState = false;
  
  // Update LED feedback every 500ms
  if (millis() - lastUpdate > 500) {
    lastUpdate = millis();
    ledState = !ledState;
    
    // Show audio playing status on LED 2
    if (audioPlaying) {
      leds[2] = ledState ? CRGB::Yellow : CRGB::Black;  // Blinking yellow = playing
    } else {
      leds[2] = CRGB::Black;  // Off = not playing
    }
    
    FastLED.show();
  }
}

// =================== COMMAND INTERFACE ===================

void handleSerialCommands() {
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    command.toLowerCase();
    
    Serial.println("📝 Command: " + command);
    
    if (command == "sd") {
      testSDCardOperations();
    } 
    else if (command == "sdinfo") {
      sdCardDiagnostics();
    }
    else if (command == "sdtest") {
      testSDCardHardware();
    }
    else if (command == "power") {
      testPowerSupply();
    }
    else if (command == "sdfix") {
      fixSDCardIssues();
    } 
    else if (command == "tone") {
      generateTestTone(440.0, 3.0); // A4 note for 3 seconds
    }
    else if (command == "wav") {
      playWAVFile("/test.wav");
    }
    else if (command == "stop") {
      stopAudioPlayback();
    }
    else if (command == "status") {
      printSystemStatus();
    }
    else if (command == "help") {
      printHelp();
    }
    else if (command.startsWith("freq ")) {
      float freq = command.substring(5).toFloat();
      if (freq > 0 && freq < 10000) {
        generateTestTone(freq, 2.0);
      } else {
        Serial.println("❌ Invalid frequency (use 20-10000 Hz)");
      }
    }
    else if (command == "sweep") {
      // Frequency sweep test
      Serial.println("🎵 Frequency sweep test (200Hz to 2000Hz)");
      audioPlaying = true; // Enable sweep mode
      for (float freq = 200; freq <= 2000 && audioPlaying; freq += 100) {
        Serial.printf("🎵 Playing %.0f Hz\n", freq);
        generateTestTone(freq, 0.5);
        delay(100);
        
        // Check for stop command during sweep
        if (Serial.available()) {
          String stopCmd = Serial.readStringUntil('\n');
          if (stopCmd.indexOf("stop") >= 0) {
            audioPlaying = false;
            Serial.println("🛑 Sweep stopped by user");
            break;
          }
        }
      }
      audioPlaying = false;
      Serial.println("✅ Frequency sweep complete");
    }
    else {
      Serial.println("❓ Unknown command. Type 'help' for available commands.");
    }
  }
}

void printSystemStatus() {
  Serial.println("\n📊 ESP32 AUDIO SYSTEM STATUS");
  Serial.println("=============================");
  Serial.println("💾 SD Card: " + String(sdCardMounted ? "✅ Mounted" : "❌ Failed"));
  Serial.println("🎵 I2S Audio: " + String(i2sInitialized ? "✅ Ready" : "❌ Failed"));
  Serial.println("🔊 Audio Playing: " + String(audioPlaying ? "Yes" : "No"));
  Serial.println("📡 Free Heap: " + String(ESP.getFreeHeap()) + " bytes");
  Serial.println("⏰ Uptime: " + String(millis() / 1000) + " seconds");
  
  if (sdCardMounted) {
    uint64_t cardSize = SD.cardSize() / (1024 * 1024);
    uint64_t usedSize = SD.usedBytes() / (1024 * 1024);
    Serial.printf("💾 SD Card: %lluMB used / %lluMB total\n", usedSize, cardSize);
  }
  
  Serial.println("🔧 Hardware Configuration:");
  Serial.println("   SD Card - MISO:19, MOSI:23, SCK:18, CS:5");
  Serial.println("   I2S DAC - DIN:22, BCLK:26, WSEL:25");
  Serial.println("   Sample Rate: " + String(I2S_SAMPLE_RATE) + " Hz");
  Serial.println("=============================");
}

void printHelp() {
  Serial.println("\n📋 ESP32 AUDIO TEST COMMANDS");
  Serial.println("=============================");
  Serial.println("🔧 Hardware Testing:");
  Serial.println("   'sd'     - Test SD card read/write operations");
  Serial.println("   'sdinfo' - Detailed SD card diagnostics and troubleshooting");
  Serial.println("   'sdtest' - Test SD card module wiring WITHOUT card inserted");
  Serial.println("   'power'  - Diagnose power supply issues");
  Serial.println("   'sdfix'  - Attempt to repair SD card issues");
  Serial.println("   'status' - Show system status and configuration");
  Serial.println("");
  Serial.println("🎵 Audio Testing:");
  Serial.println("   'tone'   - Play 440Hz test tone for 3 seconds");
  Serial.println("   'wav'    - Play test.wav file from SD card");
  Serial.println("   'sweep'  - Frequency sweep test (200-2000Hz)");
  Serial.println("   'freq X' - Play custom frequency X for 2 seconds");
  Serial.println("   'stop'   - Stop current audio playback");
  Serial.println("");
  Serial.println("📖 Information:");
  Serial.println("   'help'   - Show this help message");
  Serial.println("=============================");
  Serial.println("💡 Tips:");
  Serial.println("   - Place test.wav (44.1kHz, 16-bit stereo) on SD card");
  Serial.println("   - Use 'status' to check hardware initialization");
  Serial.println("   - Use 'sweep' to test full audio range");
}
