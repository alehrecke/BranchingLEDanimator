# How Graph-Based LED Animations Work

## The Big Picture: Two Parallel Worlds

Your LED system operates in **two parallel representations** that are kept synchronized:

```
GRAPH WORLD (Unity)          PHYSICAL WORLD (Hardware)
==================          ======================
   Node-based                   Linear LED array
   Topology-aware               Sequential addressing
   Animation logic              Hardware reality
```

## Part 1: The Graph Representation (Unity)

### What is a Graph?

From your Grasshopper export, you get:

```
NODES: Points in 3D space
  Node 0: (0.0, 136.7, 0.0)      ← Top/source node
  Node 1: (-1.5, 136.2, -2.6)
  Node 2: (-3.0, 135.7, -5.1)
  ...
  Node 49: (28.0, 0.1, 48.6)     ← Endpoint

EDGES: Connections between nodes
  Edge: Node 0 ↔ Node 1
  Edge: Node 1 ↔ Node 2
  Edge: Node 2 ↔ Node 3
  ...

TOPOLOGY: Network structure
  Node 0: Valence 3 (hub - connects to 3 branches)
  Node 1: Valence 2 (path - connects to 2 neighbors)
  Node 49: Valence 1 (endpoint - dead end)
```

### Building the Adjacency Map

When an animation starts, it builds an **adjacency dictionary**:

```csharp
Dictionary<int, List<int>> adjacency
{
  0: [1, 25, 50],      // Node 0 connects to nodes 1, 25, 50 (hub!)
  1: [0, 2],           // Node 1 connects to 0 and 2 (path)
  2: [1, 3],           // Node 2 connects to 1 and 3 (path)
  ...
  49: [48]             // Node 49 only connects to 48 (endpoint!)
}
```

This lets animations **traverse the network** like a road map!

---

## Part 2: How Animations Use the Graph

### Example 1: Breadth-First Search (BFS) Animation

**The Algorithm:**

```
1. Start at a "seed node" (usually high-valence nodes like hubs)
2. Visit all immediate neighbors (depth 1)
3. Then visit their neighbors (depth 2)
4. Continue until entire graph is explored
5. Each "wave" of exploration lights up together
```

**Visual Process:**

```
Time 0.0s:  Seed node lights up
  ●
  
Time 0.5s:  First neighbors light up (depth 1)
  ●
 ╱│╲
● ● ●

Time 1.0s:  Second neighbors light up (depth 2)
    ●
   ╱│╲
  ● ● ●
 ╱│╲│╱│╲
● ● ● ● ●

Time 1.5s:  Third neighbors light up (depth 3)
      ●
     ╱│╲
    ● ● ●
   ╱│╲│╱│╲
  ● ● ● ● ●
 ╱│╲│╱│╲│╱│╲
● ● ● ● ● ● ●
```

**The Code:**

```csharp
// BFS explores the graph layer by layer
Queue<int> queue = new Queue<int>();
queue.Enqueue(seedNode);
visited.Add(seedNode);

while (queue.Count > 0) {
    int currentNode = queue.Dequeue();
    
    // Color this node based on its depth
    colors[currentNode] = searchColor;
    
    // Add all neighbors to queue
    foreach (int neighbor in adjacency[currentNode]) {
        if (!visited.Contains(neighbor)) {
            queue.Enqueue(neighbor);
            visited.Add(neighbor);
            nodeDepths[neighbor] = nodeDepths[currentNode] + 1;
        }
    }
}
```

**Why This Creates "Branching" Effects:**

The animation doesn't know or care about the physical LED arrangement! It just follows the **topology**:

- At a hub (valence 3+), the wave splits into multiple branches
- Along a path (valence 2), the wave travels linearly
- At endpoints (valence 1), the wave stops

---

### Example 2: Ball Settle Animation

**The Algorithm:**

```
1. Balls spawn at the top node (highest Y position)
2. Each ball finds a path to a random endpoint using pathfinding
3. Ball travels along the path (node by node)
4. Ball settles at the endpoint
5. When endpoint is touched, balls scatter to new random endpoints
```

**How Pathfinding Works (BFS Pathfinding):**

```
Goal: Find path from Node 0 (top) to Node 49 (endpoint)

Step 1: Start at Node 0
  Current: 0
  Queue: []
  Parent: {0: -1}

Step 2: Explore Node 0's neighbors
  Current: 0
  Queue: [1, 25, 50]
  Parent: {0: -1, 1: 0, 25: 0, 50: 0}

Step 3: Process Node 1
  Current: 1
  Queue: [25, 50, 2]
  Parent: {0: -1, 1: 0, 2: 1, 25: 0, 50: 0}

... continue until we reach Node 49 ...

Step N: Found Node 49!
  Backtrack through parent chain:
  49 → 48 → 47 → ... → 2 → 1 → 0
  
Result: [0, 1, 2, 3, ..., 47, 48, 49]
```

**Ball Movement Along Path:**

```
Path: [0, 1, 2, 3, 4, 5]  (6 nodes)
Progress: 0.0 to 5.0 (float)

Progress 0.0:  Ball at Node 0
  ●-----○-----○-----○-----○-----○

Progress 1.5:  Ball between Node 1 and 2 (50% between)
  ○-----○--●--○-----○-----○-----○

Progress 3.0:  Ball at Node 3
  ○-----○-----○-----●-----○-----○

Progress 5.0:  Ball at Node 5 (endpoint - settled!)
  ○-----○-----○-----○-----○-----●
```

**The Code:**

```csharp
// Calculate ball position along path
float progress = (time - ball.spawnTime) * ballSpeed;
int currentNodeIndex = Mathf.FloorToInt(progress);
float fraction = progress - currentNodeIndex;

if (currentNodeIndex >= ball.path.Count - 1) {
    // Ball has reached endpoint - settle it!
    SettleBall(ball);
} else {
    // Ball is traveling between two nodes
    int nodeA = ball.path[currentNodeIndex];
    int nodeB = ball.path[currentNodeIndex + 1];
    
    // Interpolate position
    Vector3 posA = nodePositions[nodeA];
    Vector3 posB = nodePositions[nodeB];
    Vector3 ballPosition = Vector3.Lerp(posA, posB, fraction);
    
    // Light up nodes near the ball
    for (int i = 0; i < nodePositions.Count; i++) {
        float distance = Vector3.Distance(nodePositions[i], ballPosition);
        if (distance < pulseWidth) {
            float intensity = 1.0f - (distance / pulseWidth);
            colors[i] = Color.Lerp(colors[i], ballColor, intensity);
        }
    }
}
```

---

## Part 3: The Critical Mapping - Graph to Physical LEDs

This is where the magic happens! You have:

- **157 nodes** in the graph (Unity's world)
- **99 physical LEDs** in 3 strips (hardware reality)

### The LEDCircuitMapper's Job:

```
UNITY GRAPH NODES          PHYSICAL LED STRIPS
==================         ===================

Polyline 0 (50 nodes)  →   Strip 0 (33 LEDs)
  Nodes 0-49               LEDs 0-32

Polyline 1 (50 nodes)  →   Strip 1 (33 LEDs)  
  Nodes 50-99              LEDs 33-65

Polyline 2 (57 nodes)  →   Strip 2 (33 LEDs)
  Nodes 100-156            LEDs 66-98
```

### The Mapping Process:

**Step 1: Sample Nodes to LEDs**

Since you have more nodes than LEDs, we **sample** evenly:

```
Polyline 0: 50 nodes → 33 LEDs
  Sample every 50/33 = 1.515 nodes

  Node 0   → LED 0
  Node 1.5 → LED 1  (interpolate between Node 1 and 2)
  Node 3.0 → LED 2
  Node 4.5 → LED 3
  ...
  Node 49  → LED 32
```

**Step 2: Create LED Color Array**

```csharp
// Animation calculates colors for all 157 nodes
Color[] nodeColors = animation.CalculateNodeColors(...);
// Result: [color0, color1, color2, ..., color156]

// Mapper samples these to create LED array
Color[] ledColors = new Color[99];

for (int strip = 0; strip < 3; strip++) {
    for (int led = 0; led < 33; led++) {
        // Find which node(s) this LED represents
        float nodeIndex = (led / 33.0f) * polyline.nodeCount;
        
        // Sample/interpolate node colors
        ledColors[strip * 33 + led] = SampleNodeColor(nodeIndex, nodeColors);
    }
}

// Send to ESP32: [LED0, LED1, LED2, ..., LED98]
```

---

## Part 4: Why This Works for Complex Effects

### The Key Insight:

**Animations think in TOPOLOGY, not GEOMETRY!**

When a ball "travels" from Node 0 to Node 49:
1. It doesn't care that there are 50 nodes in the path
2. It doesn't care that these map to 33 physical LEDs
3. It just follows the **graph structure** step by step

### Example: Ball at Node 25 (middle of branch)

```
GRAPH VIEW (Unity):
  Ball is at Node 25
  Lights up Node 25 with full intensity
  Lights up nearby nodes (24, 26) with falloff

PHYSICAL VIEW (Hardware):
  Node 25 maps to LED 16 (approximately middle of Strip 0)
  LED 16 gets full intensity
  LEDs 15 and 17 get falloff
  
Result: Smooth traveling effect on physical LEDs!
```

### Why Branching Works:

```
GRAPH TOPOLOGY:           PHYSICAL REALITY:

    Hub (Node 0)              Strip 2 ← LED 66-98
    ╱    |    ╲               Strip 1 ← LED 33-65
Branch Branch Branch         Strip 0 ← LED 0-32
   |      |      |
Endpoint Endpoint Endpoint

When BFS reaches the hub:
- Graph: Wave splits 3 ways (follows edges)
- Physical: All 3 strips light up simultaneously
- Result: "Branching" effect appears naturally!
```

---

## Part 5: The Complete Pipeline

### Animation Frame Calculation:

```
1. ANIMATION LOGIC (Graph-based)
   ↓
   Input: Graph structure (nodes, edges, topology)
   Process: Algorithm (BFS, pathfinding, physics, etc.)
   Output: Color[] nodeColors (157 colors)

2. VISUALIZATION (Unity Display)
   ↓
   Input: nodeColors
   Process: Draw spheres at node positions
   Output: Pretty visualization in Scene/Game view

3. HARDWARE MAPPING (Physical LEDs)
   ↓
   Input: nodeColors (157) + mapping config
   Process: Sample/interpolate to LED count (99)
   Output: Color[] ledColors (99 colors)

4. COMMUNICATION (ESP32)
   ↓
   Input: ledColors (99)
   Process: UDP packet with RGB data
   Output: Physical LEDs display the animation!
```

---

## Part 6: Specific Animation Strategies

### BreadthFirstSearch Animation:

**Strategy:** Explore graph layer by layer from seed nodes

```
Key Data Structures:
- Queue<int> searchQueue         // Nodes to explore next
- HashSet<int> visited            // Already explored nodes
- Dictionary<int, int> nodeDepths // How far from seed

Algorithm:
1. Start at seed (high-valence node like hub)
2. Add seed to queue
3. While queue not empty:
   - Dequeue current node
   - Color it based on depth
   - Enqueue all unvisited neighbors
4. Result: Wave spreads through network topology
```

### BallSettle Animation:

**Strategy:** Physics-inspired particles traveling along graph paths

```
Key Data Structures:
- List<TravelingBall> travelingBalls  // Balls in motion
- List<SettledBall> settledBalls      // Balls at rest
- Each ball has: path, progress, color

Algorithm:
1. Find path from spawn to random endpoint (BFS pathfinding)
2. Ball travels along path at constant speed
3. Ball position = Lerp between path[i] and path[i+1]
4. Light up nodes near ball position with distance falloff
5. When ball reaches endpoint, add to settled list
6. On touch, dispatch all balls from that endpoint to new destinations
```

### PathFinder Animation:

**Strategy:** Interactive pathfinding with multiple simultaneous paths

```
Key Data Structures:
- List<ActivePath> activePaths    // Currently active paths
- Each path has: source, destination, pathNodes, state

States:
- Expanding: Fill traveling from source to destination
- Waiting: Fill arrived, waiting for player touch
- Receding: Fill retreating back
- Complete: Path finished

Algorithm:
1. Player touches endpoint A
2. Find path from A to random endpoint B (BFS)
3. Animate fill traveling along path (progress 0→1)
4. If player touches B, start new path from B
5. Multiple paths can be active simultaneously
```

---

## Part 7: The Rendering Process

### How a "Traveling Ball" Lights Up Multiple LEDs:

```
Ball at position (15.5, 120.3, -25.2) in 3D space

Step 1: Find nearby nodes
  for each node in graph:
    distance = Vector3.Distance(ballPosition, nodePosition)
    if distance < pulseWidth:
      // This node should be lit!

Step 2: Calculate intensity with falloff
  intensity = 1.0 - (distance / pulseWidth)
  
  Node 23: distance = 0.5  → intensity = 0.9  (very bright!)
  Node 24: distance = 2.0  → intensity = 0.5  (medium)
  Node 25: distance = 4.0  → intensity = 0.0  (dark)

Step 3: Blend colors
  colors[23] = Lerp(backgroundColor, ballColor, 0.9)
  colors[24] = Lerp(backgroundColor, ballColor, 0.5)
  colors[25] = backgroundColor

Result: Smooth glowing ball effect!
```

### How This Maps to Physical LEDs:

```
GRAPH (157 nodes with colors)
  ↓
SAMPLING (interpolate to match LED count)
  Node 23 (intensity 0.9) → LED 15 (intensity 0.9)
  Node 24 (intensity 0.5) → LED 16 (intensity 0.5)
  ↓
PHYSICAL LEDS (99 LEDs)
  LED 15: RGB(255, 100, 50) ← Bright
  LED 16: RGB(127, 50, 25)  ← Medium
  LED 17: RGB(0, 0, 0)      ← Dark
  
Result: Smooth traveling glow on physical strip!
```

---

## Part 8: The Genius of Topology-Based Animation

### Why It Works So Well:

**1. Topology Preserves Relationships**

Even though you have:
- 157 nodes in Unity
- 99 LEDs in hardware

The **relationships** are preserved:
- If Node A and Node B are neighbors in the graph
- Their corresponding LEDs will be close in the physical strip
- Animations that follow edges look smooth!

**2. Distance-Based Effects Work Naturally**

```
Ball traveling from Node 0 → Node 49:

Graph distance: 49 hops
Physical distance: ~33 LEDs on Strip 0

The ball "glows" as it moves:
- Lights up current node (full intensity)
- Lights up nearby nodes (falloff)
- Nearby in GRAPH SPACE = nearby in PHYSICAL SPACE
- Result: Smooth traveling glow!
```

**3. Branching Happens Automatically**

```
When BFS reaches a hub node:

GRAPH LOGIC:
  Hub has 3 neighbors
  → Enqueue all 3 neighbors
  → All 3 get colored on next step

PHYSICAL RESULT:
  Hub is at Node 0 (top of all 3 strips)
  → Node 1 (Strip 0) lights up
  → Node 50 (Strip 1) lights up  
  → Node 100 (Strip 2) lights up
  → Looks like wave "branches" into 3 directions!

You didn't program "branching" - it emerges from topology!
```

---

## Part 9: The Complete Data Flow

### Frame-by-Frame Process:

```
FRAME N (30 FPS = every 33ms):

1. ANIMATION CALCULATES (Graph Space)
   ├─ Input: time, frame, nodePositions, edgeConnections
   ├─ Process: Run algorithm (BFS, pathfinding, physics)
   └─ Output: Color[157] nodeColors

2. UNITY VISUALIZES (Scene/Game View)
   ├─ For each node i:
   │   └─ Draw sphere at nodePositions[i] with color nodeColors[i]
   └─ Result: Pretty visualization

3. HARDWARE MAPPING (LED Space)
   ├─ For each strip:
   │   ├─ Get polyline nodes
   │   ├─ Sample nodeColors to match LED count
   │   └─ Build Color[33] for this strip
   └─ Concatenate: Color[99] ledColors

4. ESP32 COMMUNICATION (UDP Packet)
   ├─ Build packet: [LED0_R, LED0_G, LED0_B, LED1_R, ...]
   ├─ Send UDP to ESP32 (port 8888)
   └─ ESP32 displays on physical LEDs

Total latency: ~33ms (30 FPS)
```

---

## Part 10: Why Your System is Elegant

### Traditional LED Animation:
```
Problem: "I have 99 LEDs in a line, how do I make branching effects?"
Solution: Manually program each LED index with complex math
Result: Brittle, hard to modify, geometry-specific
```

### Your Graph-Based System:
```
Problem: "I have a branching structure, how do I animate it?"
Solution: Think in topology, let mapping handle the rest
Result: Flexible, intuitive, works with any geometry!
```

### The Separation of Concerns:

```
ANIMATION LAYER:
  "Light up all neighbors of the current node"
  → Doesn't care about physical LEDs
  → Doesn't care about strip configuration
  → Just follows the graph

MAPPING LAYER:
  "Map graph colors to physical LEDs"
  → Doesn't care about animation logic
  → Just samples and interpolates
  → Handles hardware details

Result: Clean, modular, maintainable!
```

---

## Part 11: Advanced Concepts

### Distance Fields on Graphs:

Many animations use **graph distance** (hops) rather than Euclidean distance:

```
Euclidean Distance:
  Distance from Node 0 to Node 49 = 140 units (straight line in 3D)

Graph Distance:
  Distance from Node 0 to Node 49 = 49 hops (following edges)

Why graph distance matters:
- Animations follow the actual LED paths
- Effects propagate along the structure
- Creates organic, flowing animations
```

### Multi-Source Animations:

Some animations (like BFS) use multiple seed nodes:

```
Seed nodes: [0, 50, 100]  (top of each branch)

Each seed starts its own BFS:
  Search 0: Color = Red,   starts at Node 0
  Search 1: Color = Green, starts at Node 50
  Search 2: Color = Blue,  starts at Node 100

When searches meet:
  - Blend colors where they overlap
  - Creates beautiful color mixing effects
  - Visualizes graph connectivity!
```

### Velocity-Based Effects:

Ball animation tracks velocity for trail effects:

```
Ball moving from Node 10 → Node 11:
  
Current position: Node 10.7 (70% between 10 and 11)
Velocity: 0.7 nodes per frame

Trail rendering:
  - Look back along path by trailLength
  - Node 10.7: Full intensity (current position)
  - Node 10.0: 0.7 intensity (recent)
  - Node 9.0:  0.4 intensity (trail)
  - Node 8.0:  0.1 intensity (fading)
  
Result: Motion blur / comet tail effect!
```

---

## Summary: The Three Key Insights

### 1. **Graph Topology is the Animation Space**
   - Animations work with nodes and edges
   - No knowledge of physical LED arrangement needed
   - Algorithms naturally create complex effects

### 2. **Mapping Bridges Two Worlds**
   - Graph space (157 nodes) → LED space (99 LEDs)
   - Sampling and interpolation preserve smoothness
   - Topology relationships are maintained

### 3. **Emergent Complexity**
   - Simple rules (follow edges, light nearby nodes)
   - Complex geometry (branching structure)
   - Result: Sophisticated visual effects!

---

## Want to Experiment?

Try modifying these parameters to see how they affect the mapping:

1. **In BallSettleAnimation:**
   - `pulseWidth` - How many nodes around the ball light up
   - `ballSpeed` - How fast balls travel (nodes per second)
   - `trailLength` - How long the velocity trail is

2. **In LEDCircuitMapper:**
   - LED counts per strip - Changes sampling density
   - Direction (Forward/Reverse) - Flips the mapping

3. **In BreadthFirstSearch:**
   - `stepInterval` - Time between BFS layers
   - `searchSpeed` - Overall animation speed

The system will automatically adapt to your changes!
