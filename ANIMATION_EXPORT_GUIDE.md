# 🎬 Animation Export Workflow Guide

## 🎯 **Overview**

The Animation Export System enables you to design animations in Unity and deploy them for standalone playback on ESP32, without requiring a live Unity connection. This creates a complete content creation and deployment pipeline.

## 🏗️ **System Architecture**

```
Unity Design → Export Processing → ESP32 Storage → Standalone Playback
     ↓              ↓                ↓              ↓
 Live Preview → Frame Capture → SPIFFS Files → LED Display
```

### **Key Benefits:**
- ✅ **Standalone Operation**: ESP32 runs independently without Unity
- ✅ **Efficient Storage**: Optimized frame-based format
- ✅ **Playlist Management**: Multiple animations with scheduling
- ✅ **OTA Deployment**: Wireless animation updates
- ✅ **Web Interface**: Browser-based animation library management

## 🛠️ **Unity Export Tools**

### **ESP32AnimationExporter**
**Location**: `Assets/Scripts/Hardware/ESP32AnimationExporter.cs`

**Features:**
- Export current animation or entire playlist
- Configurable quality settings (frame rate, color depth)
- Real-time storage calculations
- Progress tracking with visual feedback
- Direct upload to ESP32 via existing UDP connection

**Usage:**
1. **Configure Export Settings**:
   - Target Frame Rate: 10-60 FPS
   - Color Bits Per Channel: 5-8 bits (storage vs quality trade-off)
   - Max Animation Duration: Prevents oversized exports

2. **Export Single Animation**:
   - Right-click ESP32AnimationExporter → "Export Current Animation"
   - Captures frames from currently playing animation
   - Automatically uploads to connected ESP32

3. **Export Playlist**:
   - Right-click ESP32AnimationExporter → "Export Animation Playlist" 
   - Exports all animations in current playlist
   - Creates playlist metadata for ESP32

### **ESP32PlaylistManager**
**Location**: `Assets/Scripts/Hardware/ESP32PlaylistManager.cs`

**Features:**
- Visual playlist editor in Unity Inspector
- Animation scheduling and timing controls
- Storage optimization tools
- Preview functionality for testing

**Usage:**
1. **Auto-populate from Animation System**:
   - Right-click ESP32PlaylistManager → "Populate from Animation System"
   - Automatically adds all available animations

2. **Customize Playlist Items**:
   - Override animation duration
   - Set speed multipliers
   - Enable/disable individual items
   - Set priority for weighted selection

3. **Optimize Storage**:
   - Right-click → "Calculate Playlist Storage"
   - Right-click → "Optimize Playlist" (auto-disable low priority items)

## 🔧 **ESP32 Animation Library**

### **Storage Format**
**File Structure**: `/animations/[AnimationName].anim`

**Binary Format:**
```cpp
Header: "ANIM" (4 bytes) + Version (1 byte) + Name (32 bytes) + 
        Duration (4 bytes) + FrameRate (4 bytes) + LEDCount (4 bytes) + FrameCount (4 bytes)
Frames: [Timestamp (4 bytes) + RGB Data (LEDCount × 3 bytes)] × FrameCount
```

### **Storage Capacity**
**ESP32 Internal Flash (~2.8MB available):**
- **High Quality** (30 FPS, 8-bit color): ~2-3 minutes total animation time
- **Balanced** (20 FPS, 6-bit color): ~4-5 minutes total animation time  
- **Optimized** (15 FPS, 5-bit color): ~6-8 minutes total animation time

**Per Animation Examples** (99 LEDs):
- **Wave (10s)**: ~90KB at 30 FPS
- **Graph Topology (15s)**: ~135KB at 30 FPS
- **Network Flow (20s)**: ~180KB at 30 FPS

### **Playlist System**
**File**: `/playlist.json`

**Features:**
- Loop/shuffle playback modes
- Per-animation speed control
- Enable/disable individual animations
- Priority-based weighted selection

## 🌐 **Web Interface Extensions**

### **Animation Library Page**
**URL**: `http://[ESP32_IP]/animations`

**Features:**
- Browse stored animations with details
- Preview animation metadata
- Delete animations to free storage
- Monitor storage usage with visual indicators

### **Playlist Management**
**URL**: `http://[ESP32_IP]/playlist`

**Features:**
- Drag-and-drop playlist reordering
- Enable/disable animations
- Adjust playback speed
- Start/stop standalone playback

### **Storage Monitor**
**URL**: `http://[ESP32_IP]/storage`

**Features:**
- Real-time storage usage graphs
- File system health monitoring
- Cleanup tools for old animations
- Export logs and statistics

## 📋 **Workflow Steps**

### **1. Design Phase (Unity)**
```
Unity Animation System → Design → Live Preview → Test on ESP32
```
- Create animations using existing system
- Test with live ESP32 connection
- Iterate on design and timing

### **2. Export Phase (Unity)**
```
ESP32AnimationExporter → Configure → Export → Upload
```
- Configure quality settings based on storage needs
- Export individual animations or complete playlists
- Automatic upload to connected ESP32

### **3. Deployment Phase (ESP32)**
```
SPIFFS Storage → Playlist Management → Standalone Playback
```
- Animations stored in ESP32 internal flash
- Playlist management via web interface
- Independent operation without Unity

### **4. Management Phase (Web)**
```
Web Interface → Library Browser → Playlist Editor → Playback Control
```
- Browse and manage animation library
- Create and edit playlists
- Control standalone playback

## 🎮 **Usage Examples**

### **Scenario 1: Single Animation Export**
```cpp
// In Unity:
1. Select desired animation in LEDAnimationSystem
2. Right-click ESP32AnimationExporter → "Export Current Animation"
3. Monitor export progress (visual feedback)
4. Animation automatically uploads to ESP32

// On ESP32:
- Animation stored as "/animations/WaveAnimation.anim"
- Available in web interface animation library
- Can be added to playlists or played individually
```

### **Scenario 2: Complete Playlist Deployment**
```cpp
// In Unity:
1. Configure playlist in ESP32PlaylistManager
2. Set timing and quality preferences
3. Right-click → "Export Playlist to ESP32"
4. Monitor batch export progress

// On ESP32:
- All animations stored in SPIFFS
- Playlist saved as "/playlist.json"
- Ready for standalone playback
```

### **Scenario 3: Standalone Installation**
```cpp
// Initial Setup:
1. Design and export animations in Unity
2. Disconnect ESP32 from Unity
3. Power ESP32 independently

// Standalone Operation:
- ESP32 boots and loads animation library
- Starts playlist playback automatically
- Web interface available for control
- No Unity connection required
```

## 🔧 **Configuration Options**

### **Export Quality Settings**
```cpp
// High Quality (for short animations)
Frame Rate: 30 FPS
Color Depth: 8 bits/channel
Compression: Disabled

// Balanced (recommended)
Frame Rate: 20 FPS  
Color Depth: 6 bits/channel
Compression: Enabled

// Storage Optimized (for long playlists)
Frame Rate: 15 FPS
Color Depth: 5 bits/channel  
Compression: Enabled
```

### **Playlist Scheduling**
```cpp
// Basic Loop
Loop: Enabled
Shuffle: Disabled
Transition: Fade (0.5s)

// Weighted Random
Priority-based selection
Higher priority = more frequent play
Shuffle with weights

// Time-based Rules
Schedule different playlists for different times
Weekend vs weekday content
Holiday-specific animations
```

## 🚨 **Storage Management**

### **Monitoring**
- Real-time storage usage display
- Per-animation storage breakdown
- Predictive capacity warnings
- Automatic cleanup suggestions

### **Optimization Strategies**
1. **Quality vs Storage**: Lower frame rates and color depth for longer content
2. **Selective Export**: Export only essential animations
3. **Playlist Optimization**: Auto-disable low-priority items when storage is tight
4. **Cleanup Tools**: Remove unused animations via web interface

### **Capacity Planning**
```cpp
// Formula for animation size:
Size (KB) = (Duration × Frame Rate × LED Count × 3) / 1024

// Example calculations:
Wave 10s @ 30 FPS: (10 × 30 × 99 × 3) / 1024 = 87 KB
Graph 15s @ 20 FPS: (15 × 20 × 99 × 3) / 1024 = 87 KB
Flow 20s @ 15 FPS: (20 × 15 × 99 × 3) / 1024 = 87 KB

Total: ~260 KB (about 9% of ESP32 storage)
```

## 🎯 **Future Enhancements**

### **Phase 2 Features**
- SD card support for larger libraries
- Animation compression algorithms
- Cloud sync for animation libraries
- Mobile app for remote control

### **Advanced Playlist Features**
- Sensor-triggered playlists
- Weather-responsive content
- Music synchronization
- Interactive mode selection

---

## 🚀 **Getting Started**

1. **Add Components**: Add `ESP32AnimationExporter` and `ESP32PlaylistManager` to your Unity project
2. **Design Animations**: Use existing animation system to create content
3. **Configure Export**: Set quality preferences in AnimationExporter
4. **Export & Deploy**: Use right-click menu options to export to ESP32
5. **Manage**: Use ESP32 web interface for ongoing playlist management

**Your animations are now ready for standalone deployment! 🎆**
