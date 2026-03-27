# Visual Examples: Graph Animation Process

## Example 1: Simple 3-Branch Graph

### The Graph Structure:

```
        Node 0 (Hub, Valence=3)
          /  |  \
         /   |   \
    Node 1  Node 5  Node 9
       |      |       |
    Node 2  Node 6  Node 10
       |      |       |
    Node 3  Node 7  Node 11
       |      |       |
    Node 4  Node 8  Node 12
  (Endpoint) (Endpoint) (Endpoint)
```

**Adjacency Map:**
```javascript
{
  0: [1, 5, 9],      // Hub - connects to 3 branches
  1: [0, 2],         // Path node
  2: [1, 3],         // Path node
  3: [2, 4],         // Path node
  4: [3],            // Endpoint (valence 1)
  5: [0, 6],         // Path node
  6: [5, 7],         // Path node
  7: [6, 8],         // Path node
  8: [7],            // Endpoint
  9: [0, 10],        // Path node
  10: [9, 11],       // Path node
  11: [10, 12],      // Path node
  12: [11]           // Endpoint
}
```

---

## Example 2: BFS Animation Step-by-Step

### Time 0.0s - Start at Hub

```
Colors: [BRIGHT, dark, dark, dark, dark, ...]

Visual:
        ● (Node 0 - RED)
       /|\
      / | \
     ○  ○  ○
     |  |  |
     ○  ○  ○
     |  |  |
     ○  ○  ○
     |  |  |
     ○  ○  ○
```

**Code State:**
```javascript
queue = [0]
visited = {0}
nodeDepths = {0: 0}
```

---

### Time 0.5s - First Layer (Depth 1)

```
Colors: [dim, BRIGHT, dark, dark, dark, BRIGHT, dark, dark, dark, BRIGHT, ...]

Visual:
        ○ (Node 0 - fading)
       /|\
      / | \
     ●  ●  ● (Nodes 1,5,9 - RED)
     |  |  |
     ○  ○  ○
     |  |  |
     ○  ○  ○
     |  |  |
     ○  ○  ○
```

**Code State:**
```javascript
queue = [1, 5, 9]
visited = {0, 1, 5, 9}
nodeDepths = {0: 0, 1: 1, 5: 1, 9: 1}
```

**Physical LEDs:**
```
Strip 0: [dim, BRIGHT, dark, dark, dark]  ← LED 0-4 (maps to Nodes 0-4)
Strip 1: [dim, BRIGHT, dark, dark, dark]  ← LED 5-9 (maps to Nodes 5-8)
Strip 2: [dim, BRIGHT, dark, dark, dark]  ← LED 10-14 (maps to Nodes 9-12)

Result: All 3 strips light up at position 1 - branching effect!
```

---

### Time 1.0s - Second Layer (Depth 2)

```
Visual:
        ○
       /|\
      / | \
     ○  ○  ○ (fading)
     |  |  |
     ●  ●  ● (Nodes 2,6,10 - RED)
     |  |  |
     ○  ○  ○
     |  |  |
     ○  ○  ○
```

**Code State:**
```javascript
queue = [2, 6, 10]
visited = {0, 1, 2, 5, 6, 9, 10}
nodeDepths = {0: 0, 1: 1, 2: 2, 5: 1, 6: 2, 9: 1, 10: 2}
```

**Physical LEDs:**
```
Strip 0: [dark, dim, BRIGHT, dark, dark]
Strip 1: [dark, dim, BRIGHT, dark, dark]
Strip 2: [dark, dim, BRIGHT, dark, dark]

Result: Wave travels down all 3 strips simultaneously!
```

---

## Example 3: Ball Animation Path Following

### Ball Traveling from Node 0 → Node 4

**Path Found:** [0, 1, 2, 3, 4] (5 nodes)

### Progress 0.0 - Ball at Node 0

```
Visual:
        ● (Ball here!)
        |
        ○
        |
        ○
        |
        ○
        |
        ○

Ball position: Node 0
Colors calculated:
  Node 0: Full intensity (distance = 0)
  Node 1: 0.3 intensity (distance = 2 units)
  Others: 0 intensity (too far)
```

---

### Progress 1.5 - Ball Between Nodes 1 and 2

```
Visual:
        ○
        |
        ○ ← 50% here
        ●   (Ball interpolated!)
        ○ ← 50% here
        |
        ○
        |
        ○

Ball position: Lerp(Node1.pos, Node2.pos, 0.5)
  = (Node1 + Node2) / 2

Colors calculated (pulseWidth = 4):
  Node 0: 0.2 intensity (distance = 3 units)
  Node 1: 0.8 intensity (distance = 1 unit)
  Node 2: 0.8 intensity (distance = 1 unit)
  Node 3: 0.2 intensity (distance = 3 units)
  Node 4: 0.0 intensity (distance = 5 units)

Result: Smooth glow centered between nodes!
```

---

### Progress 4.0 - Ball at Node 4 (Endpoint)

```
Visual:
        ○
        |
        ○
        |
        ○
        |
        ○
        |
        ● (Ball settled!)

Ball state: SETTLED
Added to settledBalls list
Waiting for player interaction
```

---

## Example 4: Multi-Ball Complexity

### 3 Balls with Different Paths:

```
Ball A: Path [0, 1, 2, 3, 4]     - Progress 2.5
Ball B: Path [0, 5, 6, 7, 8]     - Progress 1.0
Ball C: Path [0, 9, 10, 11, 12]  - Progress 3.5

Visual:
        ○ (Node 0 - spawn point)
       /|\
      / | \
     ○  ○  ○
     |  |  |
     ●  ●  ○  ← Ball A and B here
     |  |  |
     ○  ○  ●  ← Ball C here
     |  |  |
     ○  ○  ○

Color Calculation:
  For each node:
    color = inactiveColor
    
    For each ball:
      distance = Distance(node, ball.position)
      if distance < pulseWidth:
        intensity = 1.0 - (distance / pulseWidth)
        color = Lerp(color, ball.color, intensity)
    
    colors[node] = color

Result: Multiple glowing balls visible simultaneously!
```

---

## Example 5: The Mapping Challenge

### Your Actual Setup:

```
GRAPH:                      PHYSICAL HARDWARE:
50 nodes per branch    →    33 LEDs per strip

Node density: 50 nodes      LED density: 33 LEDs
Ratio: 50/33 = 1.515        Sample every 1.515 nodes
```

### Sampling Strategy:

```
Strip 0 LED Mapping:

LED 0  ← Node 0     (exact)
LED 1  ← Node 1.5   (interpolate between Node 1 and 2)
LED 2  ← Node 3.0   (exact)
LED 3  ← Node 4.5   (interpolate between Node 4 and 5)
...
LED 32 ← Node 49    (exact - endpoint)

Interpolation formula:
  nodeIndex = (led / 33.0) * 50.0
  nodeA = floor(nodeIndex)
  nodeB = ceil(nodeIndex)
  fraction = nodeIndex - nodeA
  
  ledColor = Lerp(nodeColors[nodeA], nodeColors[nodeB], fraction)
```

### Why Interpolation Works:

```
Ball at Node 10.7 (between Node 10 and 11):

Node 10: Color = RGB(255, 100, 50)  - Full intensity
Node 11: Color = RGB(200, 80, 40)   - Slightly dimmer

LED 7 samples Node 10.5:
  ledColor = Lerp(Node10.color, Node11.color, 0.5)
           = RGB(227, 90, 45)

Result: Smooth gradient on physical LEDs!
```

---

## Example 6: Branching Visualization

### What You See in Unity:

```
        ●  ← Node 0 lights up
       /|\
      / | \
     /  |  \
    ●   ●   ●  ← Wave splits to 3 branches
    |   |   |
    ●   ●   ●  ← Wave continues down
    |   |   |
    ●   ●   ●  ← Wave reaches endpoints
```

### What Happens on Physical LEDs:

```
Strip 0: ●●●●●●●●●●○○○○○○○○○○○○○○○○○○○○○○○
Strip 1: ●●●●●●●●●●○○○○○○○○○○○○○○○○○○○○○○○
Strip 2: ●●●●●●●●●●○○○○○○○○○○○○○○○○○○○○○○○

All 3 strips light up in parallel!
```

### The "Branching" is an Illusion:

```
REALITY: 3 separate LED strips lighting up simultaneously

PERCEPTION: Single wave splitting into 3 branches

WHY IT WORKS:
- Graph topology says "Node 0 connects to 3 branches"
- Animation follows topology
- All 3 branches get colored at the same time
- Physical strips are arranged to match graph geometry
- Your eye sees "branching"!
```

---

## Example 7: Interactive Touch Response

### PathFinder Animation Touch Sequence:

```
1. Player touches Endpoint A (Node 4)
   ↓
2. Find path to random Endpoint B (Node 8)
   Path: [4, 3, 2, 1, 0, 5, 6, 7, 8]
   ↓
3. Animate "fill" traveling along path
   Progress 0.0: ●○○○○○○○○  (at Node 4)
   Progress 0.5: ●●●●●○○○○  (halfway)
   Progress 1.0: ●●●●●●●●●  (reached Node 8)
   ↓
4. Wait for player to touch Node 8
   ↓
5. When touched, start new path from Node 8
```

**The Path Rendering:**

```csharp
// For each node in the path
for (int i = 0; i < path.Count; i++) {
    float nodePosition = i;
    float fillPosition = progress * (path.Count - 1);
    
    if (nodePosition <= fillPosition) {
        // This node is "filled"
        colors[path[i]] = fillColor;
    } else if (nodePosition < fillPosition + leadingEdgeWidth) {
        // This node is in the "leading edge"
        float edgeIntensity = 1.0 - ((nodePosition - fillPosition) / leadingEdgeWidth);
        colors[path[i]] = Lerp(inactiveColor, fillColor, edgeIntensity);
    }
}
```

**Physical Result:**

```
Strip 0 (contains Nodes 0-4):
  Progress 0.0: ○○○○●  (fill at endpoint)
  Progress 0.5: ○○●●●  (fill traveling back)
  Progress 1.0: ●●●●●  (entire branch filled)

Then continues to Strip 1:
  Progress 1.0: ●●●●●●●●○  (fill enters Strip 1)
  Progress 1.5: ●●●●●●●●●  (fill reaches endpoint)
```

---

## Example 8: Color Blending at Junctions

### Multiple Effects at Hub Node:

```
Scenario: 
- Ball A traveling through Node 0 (progress 2.0)
- Ball B traveling through Node 0 (progress 5.0)
- BFS wave at Node 0 (depth 0)

Color Calculation:
  baseColor = inactiveColor
  
  // Ball A contribution
  distanceA = Distance(Node0, BallA.position)
  intensityA = 1.0 - (distanceA / pulseWidth)
  baseColor = Lerp(baseColor, BallA.color, intensityA)
  
  // Ball B contribution
  distanceB = Distance(Node0, BallB.position)
  intensityB = 1.0 - (distanceB / pulseWidth)
  baseColor = Lerp(baseColor, BallB.color, intensityB)
  
  // BFS contribution
  baseColor = Lerp(baseColor, BFS.color, 0.5)
  
  colors[0] = baseColor

Result: Beautiful color mixing at intersection points!
```

---

## Example 9: Real-World Mapping Example

### Your Actual Geometry:

```
Grasshopper Export:
  - 3 polylines
  - 157 total nodes
  - Nodes 0-49: Branch 1 (50 nodes)
  - Nodes 50-99: Branch 2 (50 nodes)  
  - Nodes 100-156: Branch 3 (57 nodes)

Physical Hardware:
  - 3 LED strips
  - 99 total LEDs
  - Strip 0: 33 LEDs (addresses 0-32)
  - Strip 1: 33 LEDs (addresses 33-65)
  - Strip 2: 33 LEDs (addresses 66-98)
```

### The Mapping Table:

```
BRANCH 1 (50 nodes → 33 LEDs):
  Node 0   → LED 0    (hub/source)
  Node 1.5 → LED 1    (interpolated)
  Node 3.0 → LED 2
  Node 4.5 → LED 3
  ...
  Node 49  → LED 32   (endpoint)

BRANCH 2 (50 nodes → 33 LEDs):
  Node 50  → LED 33   (hub/source)
  Node 51.5 → LED 34
  ...
  Node 99  → LED 65   (endpoint)

BRANCH 3 (57 nodes → 33 LEDs):
  Node 100 → LED 66   (hub/source)
  Node 101.7 → LED 67  (interpolated - higher density!)
  ...
  Node 156 → LED 98   (endpoint)
```

**Notice:** Branch 3 has more nodes (57) for the same LEDs (33), so:
- Sampling rate: 57/33 = 1.727 nodes per LED
- Slightly lower resolution than Branches 1 and 2
- But still smooth due to interpolation!

---

## Example 10: Ball Animation Frame Breakdown

### Frame-by-Frame Analysis:

**Setup:**
- Ball spawns at Node 0 (top)
- Target: Node 4 (endpoint)
- Path: [0, 1, 2, 3, 4]
- Ball speed: 2.0 nodes/second
- Pulse width: 3.0 units

---

**Frame 0 (t=0.0s):**

```
Ball progress: 0.0
Ball at: Node 0
Ball 3D position: (0.0, 136.7, 0.0)

Distance calculation:
  Node 0: dist = 0.0   → intensity = 1.0  → RGB(255, 0, 0)
  Node 1: dist = 2.7   → intensity = 0.1  → RGB(25, 0, 0)
  Node 2: dist = 5.4   → intensity = 0.0  → RGB(0, 0, 0)

Physical LED 0: RGB(255, 0, 0) ← Bright red
Physical LED 1: RGB(25, 0, 0)  ← Dim red
Physical LED 2: RGB(0, 0, 0)   ← Off
```

---

**Frame 15 (t=0.5s):**

```
Ball progress: 0.5s × 2.0 nodes/s = 1.0
Ball at: Node 1
Ball 3D position: (-1.5, 136.2, -2.6)

Distance calculation:
  Node 0: dist = 2.7   → intensity = 0.1  → RGB(25, 0, 0)
  Node 1: dist = 0.0   → intensity = 1.0  → RGB(255, 0, 0)
  Node 2: dist = 2.7   → intensity = 0.1  → RGB(25, 0, 0)

Physical LED 0: RGB(25, 0, 0)  ← Dim (ball moved away)
Physical LED 1: RGB(255, 0, 0) ← Bright (ball here!)
Physical LED 2: RGB(25, 0, 0)  ← Dim (ball approaching)

Result: Smooth traveling glow!
```

---

**Frame 30 (t=1.0s):**

```
Ball progress: 1.0s × 2.0 nodes/s = 2.0
Ball at: Node 2
Ball 3D position: (-3.0, 135.7, -5.1)

Physical LED 0: RGB(0, 0, 0)   ← Off (ball far away)
Physical LED 1: RGB(25, 0, 0)  ← Dim
Physical LED 2: RGB(255, 0, 0) ← Bright (ball here!)
Physical LED 3: RGB(25, 0, 0)  ← Dim

Result: Ball has visibly moved down the strip!
```

---

## Example 11: Why Interpolation Matters

### Without Interpolation (Nearest Node):

```
Graph: 50 nodes → 33 LEDs

LED 10 needs a color:
  nodeIndex = (10 / 33) × 50 = 15.15
  
WITHOUT interpolation:
  Use Node 15 (round down)
  ledColor = nodeColors[15]

Result: Sudden jumps, choppy animation
```

### With Interpolation (Smooth):

```
LED 10 needs a color:
  nodeIndex = 15.15
  nodeA = 15
  nodeB = 16
  fraction = 0.15
  
WITH interpolation:
  ledColor = Lerp(nodeColors[15], nodeColors[16], 0.15)
  
Result: Smooth gradients, fluid animation!
```

**Visual Comparison:**

```
WITHOUT INTERPOLATION:
  LED: ●●●●●○○○○○●●●●●  (harsh transitions)

WITH INTERPOLATION:
  LED: ●●●●●◐◑◑◔○◔◑◑◐●●●●●  (smooth gradients)
```

---

## Example 12: The Complete Pipeline Visualized

### Single Frame Journey:

```
┌─────────────────────────────────────────────────────────────┐
│ STEP 1: ANIMATION LOGIC (Graph Space)                       │
├─────────────────────────────────────────────────────────────┤
│ Input:                                                       │
│   - nodePositions[157]    (3D positions)                    │
│   - edgeConnections       (topology)                        │
│   - time = 2.5s                                             │
│                                                              │
│ Process: BallSettleAnimation.CalculateNodeColors()          │
│   - Update ball positions along paths                       │
│   - Calculate distance from each node to each ball          │
│   - Apply intensity falloff                                 │
│   - Blend colors                                            │
│                                                              │
│ Output: nodeColors[157]                                     │
│   [RGB(255,0,0), RGB(200,0,0), RGB(100,0,0), ...]         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 2: UNITY VISUALIZATION (Scene/Game View)               │
├─────────────────────────────────────────────────────────────┤
│ LEDSceneVisualizer:                                         │
│   for i in 0..157:                                          │
│     Gizmos.DrawSphere(nodePositions[i], 0.5)               │
│     Gizmos.color = nodeColors[i]                           │
│                                                              │
│ UnifiedGameVisualizer:                                      │
│   for i in 0..157:                                          │
│     ledObjects[i].material.SetColor("_EmissionColor",      │
│                                      nodeColors[i])         │
│                                                              │
│ Result: Pretty visualization in Unity!                      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 3: HARDWARE MAPPING (LED Space)                        │
├─────────────────────────────────────────────────────────────┤
│ LEDCircuitMapper.MapNodesToLEDs():                          │
│                                                              │
│ For Strip 0 (33 LEDs):                                      │
│   polylineNodes = [0, 1, 2, ..., 49]  (50 nodes)          │
│   for led in 0..32:                                         │
│     nodeIndex = (led / 33.0) × 50.0                        │
│     ledColors[led] = SampleNodeColor(nodeIndex, nodeColors)│
│                                                              │
│ For Strip 1 (33 LEDs):                                      │
│   polylineNodes = [50, 51, ..., 99]                        │
│   for led in 33..65:                                        │
│     nodeIndex = ((led-33) / 33.0) × 50.0 + 50              │
│     ledColors[led] = SampleNodeColor(nodeIndex, nodeColors)│
│                                                              │
│ For Strip 2 (33 LEDs):                                      │
│   polylineNodes = [100, 101, ..., 156]                     │
│   for led in 66..98:                                        │
│     nodeIndex = ((led-66) / 33.0) × 57.0 + 100             │
│     ledColors[led] = SampleNodeColor(nodeIndex, nodeColors)│
│                                                              │
│ Output: ledColors[99]                                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 4: ESP32 COMMUNICATION (UDP Packet)                    │
├─────────────────────────────────────────────────────────────┤
│ ESP32Communicator.SendColorData():                          │
│                                                              │
│ Build packet:                                               │
│   byte[] packet = new byte[99 × 3];                        │
│   for i in 0..98:                                           │
│     packet[i×3 + 0] = ledColors[i].r × 255                 │
│     packet[i×3 + 1] = ledColors[i].g × 255                 │
│     packet[i×3 + 2] = ledColors[i].b × 255                 │
│                                                              │
│ Send UDP:                                                    │
│   udpClient.Send(packet, esp32IP, 8888)                    │
│                                                              │
│ ESP32 receives and displays on physical LEDs!               │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Takeaways

### 1. **Topology is Everything**
   - Animations use graph structure (nodes, edges, adjacency)
   - Physical arrangement doesn't matter to animation logic
   - Effects emerge from following the topology

### 2. **Interpolation Creates Smoothness**
   - More nodes than LEDs? Sample and interpolate
   - Preserves gradients and smooth transitions
   - Hides the resolution difference

### 3. **Separation of Concerns**
   - Animation: "What should light up and when?"
   - Mapping: "Which physical LED represents this node?"
   - Communication: "How do I send this to hardware?"

### 4. **Emergent Complexity**
   - Simple rules: follow edges, light nearby nodes
   - Complex geometry: branching structure
   - Result: Sophisticated visual effects without complex code!

---

## Next Steps to Deepen Understanding

1. **Experiment with pulseWidth in BallSettle:**
   - Set to 1.0: Tight, focused ball
   - Set to 10.0: Large, diffuse glow
   - See how it affects multiple nodes

2. **Try different LED counts in LEDCircuitMapper:**
   - 20 LEDs per strip: Lower resolution, choppier
   - 50 LEDs per strip: Higher resolution, smoother
   - See the interpolation in action

3. **Modify BFS stepInterval:**
   - 0.1s: Fast wave propagation
   - 2.0s: Slow, visible layer-by-layer growth
   - Watch how topology affects spread pattern

4. **Create a simple test animation:**
   - Light up nodes based on distance from a moving point
   - See how graph distance vs Euclidean distance differ
   - Understand why topology matters!
