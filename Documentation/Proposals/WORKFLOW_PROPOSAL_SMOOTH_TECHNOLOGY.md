# Branching LED Animator - Unity Workflow Overview

**Document Purpose**: High-level workflow for modular animation and sound integration  
**Prepared For**: Smooth Technology  
**Project**: Branching LED Sculpture Animation System  
**Date**: February 2026

---

## Overview

This document describes the **Unity-side workflow** for our LED animation system. The goal is to establish a shared understanding of how animations and audio are authored, so Smooth Technology can design hardware integration that supports our need for **creative flexibility** - specifically the ability to add, modify, and swap animations as the piece tours.

### System Context

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           RUNTIME ARCHITECTURE                              │
│                                                                             │
│     ┌───────────────┐         ┌─────────────────────────────────────┐      │
│     │               │         │      SMOOTH TECHNOLOGY HARDWARE     │      │
│     │   MAC MINI    │◄───────►│  ┌─────────┐  ┌─────────┐          │      │
│     │   + Unity     │  Network│  │ Custom  │  │ Custom  │  ...     │      │
│     │               │         │  │ Board 1 │  │ Board 2 │          │      │
│     │  (Animation   │         │  │+Sensors │  │+Sensors │          │      │
│     │   Composer)   │         │  └────┬────┘  └────┬────┘          │      │
│     │               │         │       │            │                │      │
│     └───────────────┘         │       ▼            ▼                │      │
│                               │   [LEDs]       [LEDs]               │      │
│                               │  Sculpture 1  Sculpture 2           │      │
│                               └─────────────────────────────────────┘      │
│                                                                             │
│     CREATIVE TEAM                      SMOOTH TECHNOLOGY                    │
│     RESPONSIBILITY                     RESPONSIBILITY                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Unity Animation Workflow

### The Core Concept: Modular Animation Assets

Animations are **self-contained modules** that can be created, tested, and swapped independently. Each animation is a Unity ScriptableObject asset (`.asset` file) that defines:

- Visual behavior (colors, patterns, timing)
- How it responds to the sculpture's graph topology
- Optional: how it responds to sensor input

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ANIMATION MODULE STRUCTURE                           │
│                                                                             │
│    ┌─────────────────────────────────────────────────────────────────┐     │
│    │                    Animation Asset (.asset)                     │     │
│    │                                                                 │     │
│    │   IDENTITY                                                      │     │
│    │   ├─ Name: "Wave Propagation"                                   │     │
│    │   └─ Description: "Waves emanate from branch endpoints"         │     │
│    │                                                                 │     │
│    │   VISUAL PARAMETERS                                             │     │
│    │   ├─ Primary Color          (e.g., warm orange)                 │     │
│    │   ├─ Secondary Color        (e.g., deep blue)                   │     │
│    │   ├─ Inactive Color         (e.g., black/off)                   │     │
│    │   ├─ Brightness             (0-100%)                            │     │
│    │   └─ Saturation             (0-100%)                            │     │
│    │                                                                 │     │
│    │   TIMING                                                        │     │
│    │   ├─ Speed                  (playback rate multiplier)          │     │
│    │   ├─ Duration               (cycle length in seconds)           │     │
│    │   └─ Loop                   (continuous or one-shot)            │     │
│    │                                                                 │     │
│    │   BEHAVIOR                                                      │     │
│    │   └─ CalculateColors(time, sensorData) → Color per LED          │     │
│    │                                                                 │     │
│    └─────────────────────────────────────────────────────────────────┘     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Creating a New Animation

The workflow for adding new animations:

```
    CREATIVE TEAM WORKFLOW
    ══════════════════════

    1. CREATE
       ┌────────────────────────────────────────┐
       │  Unity: Create new Animation Asset    │
       │  - Choose animation type (wave, pulse, │
       │    traversal, interactive, etc.)       │
       │  - Set visual parameters               │
       └───────────────────┬────────────────────┘
                           │
                           ▼
    2. PREVIEW
       ┌────────────────────────────────────────┐
       │  Test in Unity Scene View              │
       │  - Real-time preview on 3D model       │
       │  - Adjust parameters, see results      │
       │  - Iterate quickly without hardware    │
       └───────────────────┬────────────────────┘
                           │
                           ▼
    3. DEPLOY
       ┌────────────────────────────────────────┐
       │  Add to active animation list          │
       │  - Drag asset into playlist            │
       │  - Configure transitions/sequencing    │
       │  - System sends to hardware            │
       └────────────────────────────────────────┘
```

### Current Animation Library

| Animation | Behavior | Sensor Responsive? |
|-----------|----------|-------------------|
| **Wave Propagation** | Ripples emanate from branch endpoints | Could respond to proximity |
| **Breadth-First Search** | Graph traversal pattern, explores branches | Could trigger on touch |
| **Pulse** | Rhythmic pulsing synchronized across junctions | Could sync to audio input |
| **Shortest Path** | Highlights paths between endpoints | Could follow movement |
| **Sparkle** | Random twinkling across all branches | Ambient, always-on |
| **Network Flow** | Continuous circulation through the graph | Speed could vary with input |
| **Topology Sync** | Pulses that respect the branch structure | Could respond to presence |
| **Chord Touch** | Interactive - touch triggers harmonic response | Designed for touch input |

---

## Sound Integration

### Audio-Visual Relationship

Sound is generated based on **what the LEDs are doing**, creating tight synchronization:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         AUDIO GENERATION CONCEPT                            │
│                                                                             │
│                                                                             │
│   SCULPTURE TOPOLOGY              MUSICAL MAPPING                           │
│   ══════════════════              ═══════════════                           │
│                                                                             │
│   Branch endpoints     ───────►   Root notes (anchoring tones)              │
│   Junction nodes       ───────►   Harmonic notes (chord tones)              │
│   Mid-branch LEDs      ───────►   Passing tones (melodic movement)          │
│                                                                             │
│   High-connectivity    ───────►   Richer harmonics, longer sustain          │
│   Low-connectivity     ───────►   Simpler tones, shorter decay              │
│                                                                             │
│                                                                             │
│   ANIMATION EVENTS                AUDIO EVENTS                              │
│   ════════════════                ════════════                              │
│                                                                             │
│   LED turns on         ───────►   Chime/note triggered                      │
│   Wave passes node     ───────►   Pitch follows wave position               │
│   Pulse peak           ───────►   Rhythmic accent                           │
│   Sensor triggered     ───────►   Interactive sound response                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Audio Configuration Options

Each animation can have associated audio settings:

| Setting | Description | Example Values |
|---------|-------------|----------------|
| **Musical Scale** | Which notes are available | Pentatonic, Dorian, Lydian, Minor |
| **Root Frequency** | Base pitch for the scale | 220 Hz (A3), 261 Hz (C4) |
| **Chime Volume** | How loud the triggered notes are | 0-100% |
| **Chime Decay** | How long notes ring out | 0.5s - 3.0s |
| **Ambient Drone** | Background sustained tone | On/Off, volume level |
| **Harmonics** | Richness of the chime sounds | 1-5 harmonic overtones |

### Audio Output Options

Sound can be delivered in multiple ways:

1. **Real-time synthesis** - Unity generates audio live based on animation state
2. **Pre-rendered tracks** - Export synchronized WAV files for each animation
3. **Event stream** - Send note triggers to external audio system (MIDI-like)

---

## Data Interface

### What Unity Outputs (Per Frame)

For each animation frame, Unity calculates and can output:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PER-FRAME OUTPUT DATA                             │
│                                                                             │
│   LED COLOR ARRAY                                                           │
│   ───────────────                                                           │
│   [                                                                         │
│     { led: 0,   r: 255, g: 128, b: 0   },   // Warm orange                  │
│     { led: 1,   r: 255, g: 100, b: 0   },                                   │
│     { led: 2,   r: 200, g: 50,  b: 0   },                                   │
│     ...                                                                     │
│     { led: N-1, r: 0,   g: 0,   b: 0   }    // Off                          │
│   ]                                                                         │
│                                                                             │
│   Format: RGB values (0-255) for each LED in the sculpture                  │
│   Rate: Up to 30-60 frames per second                                       │
│                                                                             │
│                                                                             │
│   AUDIO EVENTS (Optional)                                                   │
│   ───────────────────────                                                   │
│   [                                                                         │
│     { time: 0.033, node: 85,  freq: 220.0, volume: 0.8 },                   │
│     { time: 0.066, node: 84,  freq: 246.9, volume: 0.6 },                   │
│     ...                                                                     │
│   ]                                                                         │
│                                                                             │
│   Format: Triggered notes with timing, pitch, and volume                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### What Unity Can Receive (Sensor Input)

Unity can respond to sensor data from the hardware:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          SENSOR INPUT (FROM HARDWARE)                       │
│                                                                             │
│   PROXIMITY/PRESENCE                                                        │
│   ──────────────────                                                        │
│   { sensor: "proximity_1", value: 0.75, zone: "branch_A" }                  │
│                                                                             │
│   TOUCH                                                                     │
│   ─────                                                                     │
│   { sensor: "touch_3", triggered: true, position: 0.5 }                     │
│                                                                             │
│   AMBIENT                                                                   │
│   ───────                                                                   │
│   { sensor: "light", value: 0.2 }  // Darkness level                        │
│   { sensor: "sound", value: 0.8 }  // Ambient noise level                   │
│                                                                             │
│   CUSTOM                                                                    │
│   ──────                                                                    │
│   { sensor: "custom_1", data: [...] }  // Whatever Smooth Tech defines      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Sculpture Geometry Mapping

### How LED Positions Are Defined

The sculpture geometry comes from Grasshopper/Rhino and defines:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         GEOMETRY DEFINITION                                 │
│                                                                             │
│   NODES (Points in 3D space)                                                │
│   ──────────────────────────                                                │
│   Each node has:                                                            │
│   - Position (x, y, z coordinates)                                          │
│   - Index (unique identifier)                                               │
│   - Connections (which other nodes it links to)                             │
│                                                                             │
│                                                                             │
│   BRANCHES (Polylines connecting nodes)                                     │
│   ─────────────────────────────────────                                     │
│   Each branch has:                                                          │
│   - Sequence of nodes from start to end                                     │
│   - LED count (how many physical LEDs on this branch)                       │
│   - Direction (which end is "first" in the wiring)                          │
│                                                                             │
│                                                                             │
│   SOURCE NODES (Special significance)                                       │
│   ────────────────────────────────────                                      │
│   - Branch endpoints (terminations)                                         │
│   - Junction points (where branches meet)                                   │
│   - These often serve as animation origin points                            │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Mapping Geometry to Physical LEDs

Unity maintains a mapping between the 3D model and physical LED addresses:

```
    3D MODEL                              PHYSICAL LEDS
    ════════                              ═════════════

    Branch A                              Board 1, LEDs 0-32
    (nodes 85-29)           ──────►       
                                          
    Branch B                              Board 1, LEDs 33-65
    (nodes 142-86)          ──────►       
                                          
    Branch C                              Board 2, LEDs 0-40
    (nodes 228-172)         ──────►       
    
    ...                                   ...
```

**Key Point**: This mapping is configurable. When Smooth Technology finalizes the hardware layout, we update the mapping in Unity to match.

---

## Integration Discussion Points

### Questions for Smooth Technology

1. **Communication Protocol**
   - What format should Unity send LED data? (UDP packets, serial, custom protocol?)
   - What's the expected frame rate the hardware can handle?
   - How should multi-board communication be addressed?

2. **Sensor Integration**
   - What sensor data will be available? (proximity, touch, environmental?)
   - What format will sensor data be sent to Unity?
   - What's the expected latency for sensor → Unity → LED response?

3. **Audio**
   - Should Unity generate audio, or send events to an external audio system?
   - If events, what format? (MIDI, OSC, custom?)
   - Any audio hardware on the custom boards?

4. **Deployment**
   - Can Unity push animation updates while the system is running?
   - Is there a need for "offline" mode (animations stored on hardware)?
   - How will the Mac Mini connect to the boards? (Ethernet, WiFi, USB?)

### What We Need From Smooth Technology

1. **Final LED layout** - How many LEDs per branch, physical wiring order
2. **Communication spec** - Protocol for sending LED colors to hardware
3. **Sensor spec** - What data comes back from the hardware
4. **Timing requirements** - Max latency, required frame rates

### What We Provide

1. **Animation system** - Modular, extensible, easy to add new behaviors
2. **Geometry mapping** - Flexible mapping from 3D model to physical LEDs
3. **Audio sync** - Sound generation tied to visual animation state
4. **Preview tools** - Full simulation in Unity before hardware deployment

---

## Summary

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                             │
│   CREATIVE FLEXIBILITY                    HARDWARE INTEGRATION              │
│   ════════════════════                    ════════════════════              │
│                                                                             │
│   ✓ Modular animation files               ✓ Custom board design             │
│   ✓ Easy parameter tweaking               ✓ Sensor integration              │
│   ✓ Preview without hardware              ✓ Multi-sculpture support         │
│   ✓ Add new animations anytime            ✓ Robust communication            │
│   ✓ Synchronized audio                    ✓ Power management                │
│                                                                             │
│                     ┌─────────────────────────┐                             │
│                     │                         │                             │
│                     │   INTERFACE AGREEMENT   │                             │
│                     │                         │                             │
│                     │  LED Color Data Format  │                             │
│                     │  Sensor Data Format     │                             │
│                     │  Audio Event Format     │                             │
│                     │  Communication Protocol │                             │
│                     │                         │                             │
│                     └─────────────────────────┘                             │
│                                                                             │
│   The goal: Define the interface so both teams can work independently       │
│   while ensuring seamless integration.                                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

*This document focuses on the Unity workflow and is intended to facilitate productive discussions about how the creative animation system will integrate with Smooth Technology's hardware platform.*
