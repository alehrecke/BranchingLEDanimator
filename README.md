# Branching LED Animator - Unity 6 Project

## Project Overview
A **clean, simplified** LED visualization system for complex geometries in Unity 6. Import graph structures from Rhino/Grasshopper and create custom animations with an easy-to-use modular system. Designed for ESP32 integration and physical LED control.

## ✨ Core Features
- **Complete Hardware Integration**: Full ESP32 integration with 9 specialized components
- **Manual LED Configuration**: Configure LED counts and directions for any physical setup
- **Grasshopper Import**: Direct import of polyline geometry as LED networks
- **Real-time Unity-to-ESP32 Streaming**: See animations on physical LEDs instantly (30 FPS)
- **Comprehensive Testing Suite**: Validate every component and connection
- **Dual Visualization**: Scene view (Edit mode) + Game view (3D objects with nighttime environment)
- **🎵 Audio Integration (NEW)**: SD card storage + I2S stereo audio output for synchronized LED+sound shows

## 🏗️ Complete System Architecture

### All Components (Single GameObject - 9 Components Total)
1. **LEDGraphManager**: Geometry data and Grasshopper import (single source of truth)
2. **LEDAnimationSystem**: Animation logic using modular LEDAnimationType assets
3. **LEDSceneVisualizer**: Scene view gizmos visualization (works in Edit mode)
4. **UnifiedGameVisualizer**: Game view 3D objects + nighttime environment
5. **LEDCircuitMapper**: Maps Unity nodes to physical LED strips (manual configuration)
6. **ESP32Communicator**: UDP communication with ESP32 (port 8888, custom protocol)
7. **LEDHardwareTest**: Complete testing suite for hardware validation
8. **ESP32Monitor**: Real-time ESP32 status monitoring and diagnostics
9. **LEDPhysicalMapper**: Advanced physical LED mapping tools for complex layouts

### Animation System
- **ScriptableObject-based**: Create new animations as asset files
- **Modular Design**: Easy to add new animation types (Wave, Pulse, Sparkle, etc.)
- **Real-time Updates**: Both Scene view and Game view update simultaneously
- **Event-driven**: Clean communication between components

## 📁 Clean File Structure
```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── LEDGraphManager.cs          ← Geometry data & import
│   │   ├── LEDAnimationSystem.cs       ← Animation logic
│   │   └── LEDVisualizationEvents.cs   ← Event system
│   ├── Animation/
│   │   ├── LEDAnimationType.cs         ← Base animation class
│   │   ├── WaveAnimation.cs           ← Wave animation
│   │   ├── PulseAnimation.cs          ← Pulse animation
│   │   ├── SparkleAnimation.cs        ← Sparkle animation
│   │   └── *.asset                    ← Animation asset files
│   ├── Visualization/
│   │   ├── LEDSceneVisualizer.cs      ← Scene view gizmos
│   │   └── LEDGameVisualizer.cs       ← Game view 3D objects
│   └── UI/
│       └── LEDControlPanel.cs         ← Simple unified controls
├── Data/
│   └── grasshopper_export.txt         ← Geometry data
└── Scenes/
    └── SampleScene.unity
```

## 🚀 Quick Setup (2 minutes)

### Step 1: Create Complete System
- **Menu**: `Tools → LED Animation System → Create Complete System GameObject`
- **Result**: GameObject with all 9 components created automatically

### Step 2: Import Geometry
- **Right-click LEDGraphManager** → **"Import Grasshopper Data"**
- **File**: `Assets/Data/grasshopper_export.txt` (your graph structure)

### Step 3: Create Animation Assets
- **Menu**: `Tools → LED Animation System → Create Animation Assets`
- **Drag `.asset` files** into LEDAnimationSystem's "Available Animations" list

### Step 4: Configure Physical LEDs
- **In LEDCircuitMapper**: Set LED counts per strip (e.g., 33, 33, 33)
- **Right-click LEDCircuitMapper** → **"Create LED Mapping"**

### Step 5: Connect ESP32
- **Upload**: `ESP32_Live_Controller.ino` to your ESP32
- **Set ESP32 IP** in ESP32Communicator (port 8888)
- **Right-click ESP32Communicator** → **"Connect to ESP32"**

### Step 6: Test Everything
- **Right-click ESP32Communicator** → **"Send Test Pattern - Rainbow"**
- **Result**: Physical LEDs display the test pattern!

## ✨ What You Get

### **Scene View (Edit Mode):**
- **Wireframe spheres** at LED positions with real-time colors
- **Gray connection lines** showing graph structure
- **Animated colors** changing based on current animation
- **Works immediately without Play mode!**

### **Game View (After Creating Visualization):**  
- **3D LED objects** with glow effects and emissive materials
- **Nighttime environment** with dark skybox for LED visibility
- **Same animation** as Scene view, synchronized
- **Right-click UnifiedGameVisualizer** → **"Create Game Visualization"**

### **Physical LEDs (After ESP32 Connection):**
- **Real-time streaming** of Unity animations to physical LEDs
- **30 FPS** smooth animation playback
- **Configurable LED counts** - works with any strip configuration
- **Test patterns** for validation and troubleshooting

## 🎨 Creating New Animations

### Easy Method:
1. **Right-click in Project** → `Assets → Create → LED Animation → [Type]`
2. **Configure colors, speed, etc.** in Inspector
3. **Drag asset** into animation list

### Code Method:
```csharp
[CreateAssetMenu(menuName = "LED Animation/My Animation")]
public class MyAnimation : LEDAnimationType
{
    public override Color[] CalculateNodeColors(
        List<Vector3> nodePositions,
        List<Vector2Int> edgeConnections, 
        List<int> sourceNodes,
        float time, int frame)
    {
        // Your animation logic here
        Color[] colors = new Color[nodePositions.Count];
        // ... calculate colors based on time/frame
        return colors;
    }
}
```

## 🔌 Complete ESP32 Hardware Integration

### Hardware Components:
- **LEDCircuitMapper**: Maps Unity nodes to physical LED strips (manual configuration)
- **ESP32Communicator**: UDP communication using custom protocol (port 8888)
- **LEDHardwareTest**: Comprehensive testing system for validation
- **ESP32Monitor**: Real-time status monitoring and diagnostics

### Compatible with Your Setup:
- ✅ **Configurable LED counts** (you set LED counts per strip manually)
- ✅ **GPIO 19 data pin** (ESP32 standard wiring)
- ✅ **Custom UDP protocol** (no ESP32 code changes needed)
- ✅ **Real-time streaming** (30 FPS Unity-to-ESP32)

### Get LED Data:
```csharp
LEDGraphManager graphManager = GetComponent<LEDGraphManager>();
ESP32Communicator esp32 = GetComponent<ESP32Communicator>();

// Real-time color streaming to ESP32
// Automatic mapping from Unity nodes to physical LED addresses
// Comprehensive testing and monitoring tools
```

## 🎯 Key Benefits

**Complete Integration:**
- **9 specialized components** handling every aspect
- **Hardware-first design** - built for real LED installations
- **Manual LED configuration** - works with any physical setup

**Production Ready:**  
- **Real-time streaming** at 30 FPS to physical LEDs
- **Comprehensive testing** suite validates entire pipeline
- **Professional debugging** tools and status monitoring

**User-Friendly:**
- **Single GameObject** setup - no complex dependencies
- **Context menu actions** - right-click to access all functions
- **Immediate feedback** - see results instantly in Scene view

**Future-Ready:**
- **Playlist system preparation** for sound synchronization
- **Advanced mapping tools** for complex LED arrangements
- **Modular architecture** for continuous feature expansion

---

## 🔧 Manual LED Configuration System

### Configure Your Physical Setup:
Unlike automated systems, you manually configure LED counts to match your hardware:

**In LEDCircuitMapper Inspector:**
```
Strip 0: 33 LEDs, Forward direction, Order 0
Strip 1: 33 LEDs, Forward direction, Order 1  
Strip 2: 33 LEDs, Reverse direction, Order 2
```

**Physical Setup Example:**
```
Your Physical LEDs: [Strip0: 0-32][Strip1: 33-65][Strip2: 66-98]
Unity Graph Nodes:  157 nodes mapped to 99 physical LED positions
ESP32 Receives:     Single continuous stream of 99 RGB values
```

### Hardware Requirements:
- **ESP32 with WiFi** (any development board)
- **WS2812B LED strips** (configurable count per strip)
- **5V power supply** (adequate for your LED count)
- **GPIO 19** for LED data (or configure in firmware)

### Firmware:
- **Use**: `ESP32_Live_Controller.ino` (for Unity connectivity)
- **Port**: 8888 (fixed, matches Unity)
- **Protocol**: Custom UDP format (automatically handled)

### Testing Suite:
The system includes comprehensive testing:
- **Component Reference Test**: Verify all components linked
- **LED Mapping Test**: Validate Unity-to-physical mapping
- **ESP32 Connection Test**: Confirm UDP communication
- **Physical LED Test**: Send test patterns to verify hardware
- **Live Animation Test**: Stream real-time animations

---

## 📖 Documentation

- **`CLEAN_SETUP_GUIDE.md`** - Complete setup instructions for 9-component system
- **`ESP32_SETUP_GUIDE.md`** - Hardware setup and LED configuration
- **`DUAL_FIRMWARE_GUIDE.md`** - ESP32 firmware options (Live vs Standalone)
- **`ANIMATION_EXPORT_GUIDE.md`** - Future playlist and sound sync features
- **`SIMPLIFIED_ARCHITECTURE.md`** - Technical architecture overview
- **`AUDIO_INTEGRATION_ROADMAP.md`** - 🎵 **NEW**: Audio hardware integration guide and roadmap

## ⚡ Keyboard Shortcuts
- **Tab** - Toggle UI
- **Space** - Play/Pause animation
- **R** - Reset animation

---

**The system provides complete hardware integration while maintaining the simplicity of single GameObject setup. Ready for current LED installations and future playlist/sound features!** 🎬✨