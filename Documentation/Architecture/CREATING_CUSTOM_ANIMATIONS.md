# Creating Custom Graph-Based Animations

This guide shows you how to create your own animations by understanding the graph-based approach.

---

## Template: Simple Distance-Based Animation

Here's a minimal animation that demonstrates the core concepts:

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "SimpleWave", menuName = "LED Animations/Tutorial/Simple Wave")]
    public class SimpleWaveAnimation : LEDAnimationType
    {
        [Header("Wave Settings")]
        public float waveSpeed = 5f;
        public float waveWidth = 10f;
        public Color waveColor = Color.cyan;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Wave travels up and down based on Y position
            float wavePosition = Mathf.Sin(time * waveSpeed) * 50f + 70f;
            
            for (int i = 0; i < nodePositions.Count; i++)
            {
                float nodeHeight = nodePositions[i].y;
                float distanceFromWave = Mathf.Abs(nodeHeight - wavePosition);
                
                if (distanceFromWave < waveWidth)
                {
                    float intensity = 1f - (distanceFromWave / waveWidth);
                    colors[i] = Color.Lerp(inactiveColor, waveColor, intensity);
                }
                else
                {
                    colors[i] = inactiveColor;
                }
            }
            
            return colors;
        }
    }
}
```

**What this does:**
- Creates a wave that moves up and down based on node height
- Doesn't use topology at all - just position
- Simple but effective!

---

## Example 1: Topology-Aware Pulse

Let's make an animation that follows the graph structure:

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "TopologyPulse", menuName = "LED Animations/Tutorial/Topology Pulse")]
    public class TopologyPulseAnimation : LEDAnimationType
    {
        [Header("Pulse Settings")]
        public float pulseSpeed = 2f;
        public float pulseWidth = 5f;
        public Color pulseColor = Color.magenta;
        
        // Cache graph structure
        private Dictionary<int, List<int>> adjacency;
        private Dictionary<int, int> graphDistances;
        private bool initialized = false;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            // Build adjacency map on first frame
            if (!initialized)
            {
                BuildAdjacency(nodePositions.Count, edgeConnections);
                CalculateGraphDistances(sourceNodes.Count > 0 ? sourceNodes[0] : 0);
                initialized = true;
            }
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Pulse travels based on GRAPH DISTANCE, not Euclidean distance
            float pulsePosition = (time * pulseSpeed) % 50f; // Loop every 50 hops
            
            for (int i = 0; i < nodePositions.Count; i++)
            {
                if (!graphDistances.ContainsKey(i)) continue;
                
                float nodeDistance = graphDistances[i];
                float distanceFromPulse = Mathf.Abs(nodeDistance - pulsePosition);
                
                if (distanceFromPulse < pulseWidth)
                {
                    float intensity = 1f - (distanceFromPulse / pulseWidth);
                    colors[i] = Color.Lerp(inactiveColor, pulseColor, intensity);
                }
                else
                {
                    colors[i] = inactiveColor;
                }
            }
            
            return colors;
        }
        
        void BuildAdjacency(int nodeCount, List<Vector2Int> edges)
        {
            adjacency = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < nodeCount; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edges)
            {
                if (!adjacency[edge.x].Contains(edge.y))
                    adjacency[edge.x].Add(edge.y);
                if (!adjacency[edge.y].Contains(edge.x))
                    adjacency[edge.y].Add(edge.x);
            }
        }
        
        void CalculateGraphDistances(int sourceNode)
        {
            // BFS to calculate distance from source to all nodes
            graphDistances = new Dictionary<int, int>();
            Queue<int> queue = new Queue<int>();
            
            queue.Enqueue(sourceNode);
            graphDistances[sourceNode] = 0;
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int currentDist = graphDistances[current];
                
                foreach (int neighbor in adjacency[current])
                {
                    if (!graphDistances.ContainsKey(neighbor))
                    {
                        graphDistances[neighbor] = currentDist + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }
}
```

**What this demonstrates:**
- Uses **graph distance** (hops) instead of Euclidean distance
- Pulse follows the network structure
- Automatically creates branching effect!

**Comparison:**

```
EUCLIDEAN DISTANCE:
  Straight line through 3D space
  Ignores the network structure
  
GRAPH DISTANCE:
  Follows edges/connections
  Respects the topology
  Creates organic flow along branches
```

---

## Example 2: Multi-Source Ripple

Create waves from multiple points that interact:

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "MultiRipple", menuName = "LED Animations/Tutorial/Multi Ripple")]
    public class MultiRippleAnimation : LEDAnimationType
    {
        [Header("Ripple Settings")]
        public int numberOfSources = 3;
        public float rippleSpeed = 3f;
        public float rippleWidth = 8f;
        
        private Dictionary<int, List<int>> adjacency;
        private List<int> rippleSources;
        private List<Dictionary<int, int>> sourceDistances;
        private bool initialized = false;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            if (!initialized)
            {
                BuildAdjacency(nodePositions.Count, edgeConnections);
                PickRandomSources(nodePositions.Count);
                CalculateAllDistances();
                initialized = true;
            }
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize all nodes to inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Create ripple from each source
            for (int s = 0; s < rippleSources.Count; s++)
            {
                float ripplePosition = (time * rippleSpeed + s * 10f) % 50f;
                Color rippleColor = GetRippleColor(s);
                
                var distances = sourceDistances[s];
                
                foreach (var kvp in distances)
                {
                    int nodeIndex = kvp.Key;
                    float nodeDistance = kvp.Value;
                    float distanceFromRipple = Mathf.Abs(nodeDistance - ripplePosition);
                    
                    if (distanceFromRipple < rippleWidth)
                    {
                        float intensity = 1f - (distanceFromRipple / rippleWidth);
                        // Additive blending for overlapping ripples
                        colors[nodeIndex] = colors[nodeIndex] + rippleColor * intensity;
                    }
                }
            }
            
            return colors;
        }
        
        Color GetRippleColor(int sourceIndex)
        {
            float hue = (sourceIndex / (float)numberOfSources);
            return Color.HSVToRGB(hue, 0.8f, 1f);
        }
        
        void PickRandomSources(int nodeCount)
        {
            rippleSources = new List<int>();
            for (int i = 0; i < numberOfSources; i++)
            {
                rippleSources.Add(Random.Range(0, nodeCount));
            }
        }
        
        void CalculateAllDistances()
        {
            sourceDistances = new List<Dictionary<int, int>>();
            
            foreach (int source in rippleSources)
            {
                var distances = CalculateDistancesFrom(source);
                sourceDistances.Add(distances);
            }
        }
        
        Dictionary<int, int> CalculateDistancesFrom(int sourceNode)
        {
            var distances = new Dictionary<int, int>();
            var queue = new Queue<int>();
            
            queue.Enqueue(sourceNode);
            distances[sourceNode] = 0;
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int currentDist = distances[current];
                
                if (adjacency.ContainsKey(current))
                {
                    foreach (int neighbor in adjacency[current])
                    {
                        if (!distances.ContainsKey(neighbor))
                        {
                            distances[neighbor] = currentDist + 1;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            return distances;
        }
        
        void BuildAdjacency(int nodeCount, List<Vector2Int> edges)
        {
            adjacency = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < nodeCount; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edges)
            {
                if (!adjacency[edge.x].Contains(edge.y))
                    adjacency[edge.x].Add(edge.y);
                if (!adjacency[edge.y].Contains(edge.x))
                    adjacency[edge.y].Add(edge.x);
            }
        }
    }
}
```

**What this creates:**
- Multiple ripples emanating from random points
- Each ripple follows graph topology
- Ripples blend where they overlap
- Creates complex interference patterns!

---

## Example 3: Endpoint-to-Endpoint Flow

Animate energy flowing between endpoints:

```csharp
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "EndpointFlow", menuName = "LED Animations/Tutorial/Endpoint Flow")]
    public class EndpointFlowAnimation : LEDAnimationType
    {
        [Header("Flow Settings")]
        public float flowSpeed = 2f;
        public float flowWidth = 5f;
        public float timeBetweenFlows = 3f;
        
        private Dictionary<int, List<int>> adjacency;
        private List<int> endpoints;
        private List<FlowPath> activeFlows;
        private float lastFlowTime = 0f;
        private bool initialized = false;
        
        private class FlowPath
        {
            public List<int> path;
            public float startTime;
            public Color color;
        }
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            if (!initialized)
            {
                BuildAdjacency(nodePositions.Count, edgeConnections);
                FindEndpoints();
                activeFlows = new List<FlowPath>();
                initialized = true;
            }
            
            // Spawn new flow periodically
            if (time - lastFlowTime > timeBetweenFlows && endpoints.Count >= 2)
            {
                SpawnNewFlow(time);
                lastFlowTime = time;
            }
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Render all active flows
            foreach (var flow in activeFlows.ToList())
            {
                float elapsed = time - flow.startTime;
                float progress = elapsed * flowSpeed;
                
                // Remove completed flows
                if (progress >= flow.path.Count)
                {
                    activeFlows.Remove(flow);
                    continue;
                }
                
                // Render flow along path
                for (int i = 0; i < flow.path.Count; i++)
                {
                    float distanceFromFlow = Mathf.Abs(i - progress);
                    
                    if (distanceFromFlow < flowWidth)
                    {
                        float intensity = 1f - (distanceFromFlow / flowWidth);
                        int nodeIndex = flow.path[i];
                        colors[nodeIndex] = Color.Lerp(colors[nodeIndex], flow.color, intensity);
                    }
                }
            }
            
            // Highlight endpoints
            foreach (int endpoint in endpoints)
            {
                float pulse = 0.3f + 0.2f * Mathf.Sin(time * 2f);
                colors[endpoint] = Color.Lerp(colors[endpoint], primaryColor, pulse);
            }
            
            return colors;
        }
        
        void SpawnNewFlow(float time)
        {
            // Pick random source and destination endpoints
            int sourceEndpoint = endpoints[Random.Range(0, endpoints.Count)];
            int destEndpoint = endpoints[Random.Range(0, endpoints.Count)];
            
            if (sourceEndpoint == destEndpoint) return;
            
            // Find path between them
            List<int> path = FindPath(sourceEndpoint, destEndpoint);
            
            if (path != null && path.Count > 0)
            {
                var flow = new FlowPath
                {
                    path = path,
                    startTime = time,
                    color = Color.HSVToRGB(Random.value, 0.8f, 1f)
                };
                
                activeFlows.Add(flow);
                Debug.Log($"Flow started: {sourceEndpoint} → {destEndpoint} ({path.Count} nodes)");
            }
        }
        
        void FindEndpoints()
        {
            endpoints = new List<int>();
            
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1) // Valence 1 = endpoint
                {
                    endpoints.Add(kvp.Key);
                }
            }
            
            Debug.Log($"Found {endpoints.Count} endpoints");
        }
        
        List<int> FindPath(int start, int end)
        {
            Queue<int> queue = new Queue<int>();
            Dictionary<int, int> parent = new Dictionary<int, int>();
            HashSet<int> visited = new HashSet<int>();
            
            queue.Enqueue(start);
            visited.Add(start);
            parent[start] = -1;
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                
                if (current == end)
                {
                    // Reconstruct path
                    List<int> path = new List<int>();
                    int node = end;
                    while (node != -1)
                    {
                        path.Add(node);
                        node = parent[node];
                    }
                    path.Reverse();
                    return path;
                }
                
                if (adjacency.ContainsKey(current))
                {
                    foreach (int neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            parent[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            return null; // No path found
        }
        
        void BuildAdjacency(int nodeCount, List<Vector2Int> edges)
        {
            adjacency = new Dictionary<int, List<int>>();
            
            for (int i = 0; i < nodeCount; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edges)
            {
                if (!adjacency[edge.x].Contains(edge.y))
                    adjacency[edge.x].Add(edge.y);
                if (!adjacency[edge.y].Contains(edge.x))
                    adjacency[edge.y].Add(edge.x);
            }
        }
    }
}
```

**What this demonstrates:**
- Finding endpoints by checking valence
- Pathfinding between arbitrary nodes
- Multiple simultaneous flows
- Topology-aware animation!

---

## Example 4: Understanding the Ball Settle Logic

Let's break down the key parts of BallSettleAnimation:

### Part A: Finding the Top Node

```csharp
// Find the highest node in the graph (spawn point for balls)
float maxHeight = float.MinValue;
int topNode = -1;

for (int i = 0; i < nodePositions.Count; i++)
{
    if (nodePositions[i].y > maxHeight)
    {
        maxHeight = nodePositions[i].y;
        topNode = i;
    }
}

Debug.Log($"Top node: {topNode} at height {maxHeight}");
```

**Why this matters:**
- Balls spawn at the "top" of your structure
- Uses actual 3D geometry (Y position)
- Gives balls somewhere logical to start

---

### Part B: Spawning a Ball

```csharp
void SpawnBall(float time)
{
    // Pick random endpoint as destination
    int targetEndpoint = endpoints[Random.Range(0, endpoints.Count)];
    
    // Find path from top to this endpoint
    List<int> path = FindPath(topNode, targetEndpoint);
    
    if (path == null || path.Count == 0)
    {
        Debug.LogWarning("No path found!");
        return;
    }
    
    // Create ball
    float hue = nextBallIndex / (float)totalBalls; // Rainbow colors
    var ball = new TravelingBall(path, time, hue, nextBallIndex);
    
    travelingBalls.Add(ball);
    nextBallIndex++;
    
    Debug.Log($"Ball {ball.ballIndex} spawned: {path.Count} nodes to endpoint {targetEndpoint}");
}
```

**Key insight:**
- Path is just a list of node indices: [0, 1, 2, 3, 4]
- Ball doesn't know about 3D positions yet
- That comes during rendering!

---

### Part C: Moving the Ball

```csharp
void UpdateTravelingBalls(float time, float deltaTime)
{
    foreach (var ball in travelingBalls.ToList())
    {
        // How far along the path?
        float elapsed = time - ball.spawnTime - ball.startDelay;
        if (elapsed < 0) continue; // Not started yet
        
        ball.progress = elapsed * ballSpeed;
        
        // Has ball reached the end?
        if (ball.progress >= ball.path.Count - 1)
        {
            // Settle the ball
            var settled = new SettledBall(ball.targetEndpoint, ball.hue, ball.ballIndex, time);
            settledBalls.Add(settled);
            travelingBalls.Remove(ball);
            
            Debug.Log($"Ball {ball.ballIndex} settled at endpoint {ball.targetEndpoint}");
        }
    }
}
```

**The progress variable:**
```
progress = 0.0   → Ball at path[0]
progress = 1.5   → Ball 50% between path[1] and path[2]
progress = 4.0   → Ball at path[4]
progress = 4.99  → Ball 99% to path[5] (almost there!)
progress >= 5.0  → Ball has arrived (if path has 6 nodes)
```

---

### Part D: Rendering the Ball

```csharp
void RenderTravelingBalls(Color[] colors, List<Vector3> nodePositions)
{
    foreach (var ball in travelingBalls)
    {
        // Get current position along path
        int currentIndex = Mathf.FloorToInt(ball.progress);
        float fraction = ball.progress - currentIndex;
        
        if (currentIndex >= ball.path.Count - 1)
        {
            currentIndex = ball.path.Count - 2;
            fraction = 1f;
        }
        
        // Interpolate 3D position
        int nodeA = ball.path[currentIndex];
        int nodeB = ball.path[currentIndex + 1];
        Vector3 posA = nodePositions[nodeA];
        Vector3 posB = nodePositions[nodeB];
        Vector3 ballPosition = Vector3.Lerp(posA, posB, fraction);
        
        // Get ball color
        Color ballColor = Color.HSVToRGB(ball.hue, rainbowSaturation, rainbowBrightness);
        
        // Light up nearby nodes
        for (int i = 0; i < nodePositions.Count; i++)
        {
            float distance = Vector3.Distance(nodePositions[i], ballPosition);
            
            if (distance < pulseWidth)
            {
                float intensity = 1f - (distance / pulseWidth);
                intensity = pulseShape.Evaluate(intensity); // Apply curve
                
                // Blend with existing color
                colors[i] = Color.Lerp(colors[i], ballColor, intensity);
            }
        }
        
        // Optional: Render velocity trail
        if (pulseStyle == PulseStyle.VelocityTrail)
        {
            RenderVelocityTrail(ball, colors, nodePositions, ballColor);
        }
    }
}
```

**The magic moment:**
```csharp
Vector3 ballPosition = Vector3.Lerp(posA, posB, fraction);
```

This single line:
- Takes two node positions (3D points)
- Interpolates between them
- Creates smooth motion between discrete nodes!

---

### Part E: Velocity Trail

```csharp
void RenderVelocityTrail(TravelingBall ball, Color[] colors, List<Vector3> nodePositions, Color ballColor)
{
    // Look back along the path
    float trailStart = ball.progress - trailLength;
    
    for (float t = trailStart; t < ball.progress; t += 0.5f)
    {
        if (t < 0) continue;
        
        int index = Mathf.FloorToInt(t);
        if (index >= ball.path.Count - 1) continue;
        
        float frac = t - index;
        int nodeA = ball.path[index];
        int nodeB = ball.path[index + 1];
        
        Vector3 posA = nodePositions[nodeA];
        Vector3 posB = nodePositions[nodeB];
        Vector3 trailPosition = Vector3.Lerp(posA, posB, frac);
        
        // Trail intensity fades with distance from ball
        float trailProgress = (t - trailStart) / trailLength;
        float trailIntensity = trailProgress * trailIntensity;
        
        // Light up nodes near this trail point
        for (int i = 0; i < nodePositions.Count; i++)
        {
            float distance = Vector3.Distance(nodePositions[i], trailPosition);
            
            if (distance < pulseWidth * 0.5f)
            {
                float intensity = (1f - distance / (pulseWidth * 0.5f)) * trailIntensity;
                colors[i] = Color.Lerp(colors[i], ballColor, intensity);
            }
        }
    }
}
```

**Visual result:**
```
Ball with trail:
  ○○○◔◑◐●  (comet tail effect)
  
Without trail:
  ○○○○○○●  (just the ball)
```

---

## Common Patterns and Utilities

### Pattern 1: Build Adjacency Map

**Always needed for topology-based animations:**

```csharp
Dictionary<int, List<int>> BuildAdjacency(int nodeCount, List<Vector2Int> edges)
{
    var adjacency = new Dictionary<int, List<int>>();
    
    for (int i = 0; i < nodeCount; i++)
    {
        adjacency[i] = new List<int>();
    }
    
    foreach (var edge in edges)
    {
        // Undirected graph - add both directions
        if (!adjacency[edge.x].Contains(edge.y))
            adjacency[edge.x].Add(edge.y);
        if (!adjacency[edge.y].Contains(edge.x))
            adjacency[edge.y].Add(edge.x);
    }
    
    return adjacency;
}
```

---

### Pattern 2: Calculate Graph Distances (BFS)

**Find shortest path distance from one node to all others:**

```csharp
Dictionary<int, int> CalculateDistances(int sourceNode, Dictionary<int, List<int>> adjacency)
{
    var distances = new Dictionary<int, int>();
    var queue = new Queue<int>();
    
    queue.Enqueue(sourceNode);
    distances[sourceNode] = 0;
    
    while (queue.Count > 0)
    {
        int current = queue.Dequeue();
        int currentDist = distances[current];
        
        foreach (int neighbor in adjacency[current])
        {
            if (!distances.ContainsKey(neighbor))
            {
                distances[neighbor] = currentDist + 1;
                queue.Enqueue(neighbor);
            }
        }
    }
    
    return distances;
}
```

**Usage:**
```csharp
var distances = CalculateDistances(0, adjacency);
// distances[10] = 10  (Node 10 is 10 hops from Node 0)
// distances[49] = 49  (Node 49 is 49 hops from Node 0)
```

---

### Pattern 3: Find Path Between Two Nodes

**BFS pathfinding:**

```csharp
List<int> FindPath(int start, int end, Dictionary<int, List<int>> adjacency)
{
    Queue<int> queue = new Queue<int>();
    Dictionary<int, int> parent = new Dictionary<int, int>();
    HashSet<int> visited = new HashSet<int>();
    
    queue.Enqueue(start);
    visited.Add(start);
    parent[start] = -1;
    
    while (queue.Count > 0)
    {
        int current = queue.Dequeue();
        
        if (current == end)
        {
            // Reconstruct path by following parent chain
            List<int> path = new List<int>();
            int node = end;
            while (node != -1)
            {
                path.Add(node);
                node = parent[node];
            }
            path.Reverse();
            return path;
        }
        
        foreach (int neighbor in adjacency[current])
        {
            if (!visited.Contains(neighbor))
            {
                visited.Add(neighbor);
                parent[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }
    }
    
    return null; // No path exists
}
```

---

### Pattern 4: Classify Nodes by Valence

**Identify hubs, paths, and endpoints:**

```csharp
void ClassifyNodes(Dictionary<int, List<int>> adjacency)
{
    List<int> hubs = new List<int>();       // Valence >= 3
    List<int> paths = new List<int>();      // Valence == 2
    List<int> endpoints = new List<int>();  // Valence == 1
    
    foreach (var kvp in adjacency)
    {
        int nodeIndex = kvp.Key;
        int valence = kvp.Value.Count;
        
        if (valence >= 3)
            hubs.Add(nodeIndex);
        else if (valence == 2)
            paths.Add(nodeIndex);
        else if (valence == 1)
            endpoints.Add(nodeIndex);
    }
    
    Debug.Log($"Graph structure: {hubs.Count} hubs, {paths.Count} path nodes, {endpoints.Count} endpoints");
}
```

**Why this is useful:**
- Hubs are good spawn points for spreading effects
- Endpoints are good targets for pathfinding
- Path nodes can be treated differently (e.g., faster travel)

---

### Pattern 5: Distance-Based Glow

**Light up nodes near a 3D point:**

```csharp
void ApplyGlowAtPosition(Vector3 glowPosition, Color glowColor, float glowRadius, 
                         Color[] colors, List<Vector3> nodePositions)
{
    for (int i = 0; i < nodePositions.Count; i++)
    {
        float distance = Vector3.Distance(nodePositions[i], glowPosition);
        
        if (distance < glowRadius)
        {
            float intensity = 1f - (distance / glowRadius);
            
            // Optional: Apply easing curve
            intensity = Mathf.Pow(intensity, 2f); // Quadratic falloff
            
            colors[i] = Color.Lerp(colors[i], glowColor, intensity);
        }
    }
}
```

**Falloff curves:**
```
Linear:     intensity = 1 - (d / r)
Quadratic:  intensity = (1 - d/r)²
Smooth:     intensity = smoothstep(0, 1, 1 - d/r)

Visual:
Linear:    ●◐◑◔○  (even falloff)
Quadratic: ●◐◔○○  (sharper, more focused)
Smooth:    ●◐◑○○  (natural, organic)
```

---

## Debugging Your Animations

### Visualize What's Happening:

```csharp
protected override Color[] CalculateBaseNodeColors(...)
{
    // ... your animation logic ...
    
    // DEBUG: Print state every second
    if (frame % 30 == 0)
    {
        Debug.Log($"Frame {frame}, Time {time:F2}s");
        Debug.Log($"  Active balls: {travelingBalls.Count}");
        Debug.Log($"  Settled balls: {settledBalls.Count}");
        
        if (travelingBalls.Count > 0)
        {
            var ball = travelingBalls[0];
            Debug.Log($"  Ball 0: progress={ball.progress:F2}, path length={ball.path.Count}");
        }
    }
    
    return colors;
}
```

### Visualize Graph Structure:

```csharp
[ContextMenu("Debug Graph Structure")]
void DebugGraphStructure()
{
    if (adjacency == null) return;
    
    Debug.Log("=== GRAPH STRUCTURE ===");
    
    foreach (var kvp in adjacency)
    {
        int node = kvp.Key;
        int valence = kvp.Value.Count;
        string type = valence == 1 ? "ENDPOINT" : 
                     valence == 2 ? "PATH" : 
                     "HUB";
        
        Debug.Log($"Node {node}: {type} (valence {valence}) - neighbors: {string.Join(",", kvp.Value)}");
    }
}
```

---

## Performance Tips

### 1. Cache Expensive Calculations

```csharp
// BAD: Recalculate every frame
protected override Color[] CalculateBaseNodeColors(...)
{
    var adjacency = BuildAdjacency(...);  // Expensive!
    var distances = CalculateDistances(...);  // Expensive!
    // ... use them ...
}

// GOOD: Calculate once, cache
private Dictionary<int, List<int>> adjacency;
private Dictionary<int, int> distances;
private bool initialized = false;

protected override Color[] CalculateBaseNodeColors(...)
{
    if (!initialized)
    {
        adjacency = BuildAdjacency(...);
        distances = CalculateDistances(...);
        initialized = true;
    }
    // ... use cached data ...
}
```

---

### 2. Limit Distance Checks

```csharp
// BAD: Check every node against every ball
for (int i = 0; i < nodeCount; i++)
{
    foreach (var ball in balls)
    {
        float dist = Distance(nodes[i], ball.position);
        // O(n × m) - expensive!
    }
}

// GOOD: Only check nodes near the ball's path
foreach (var ball in balls)
{
    int pathIndex = Mathf.FloorToInt(ball.progress);
    
    // Only check nodes in path segment ± range
    int startCheck = Mathf.Max(0, pathIndex - 5);
    int endCheck = Mathf.Min(ball.path.Count, pathIndex + 5);
    
    for (int i = startCheck; i < endCheck; i++)
    {
        int nodeIndex = ball.path[i];
        // Only checking ~10 nodes instead of 157!
    }
}
```

---

## Experimentation Ideas

### 1. **Gravity-Based Ball Animation**
   - Balls accelerate as they fall (variable speed)
   - Use node Y position to calculate "gravity"
   - Balls move faster at bottom, slower at top

### 2. **Contagion Spread**
   - Touch one node to "infect" it
   - Infection spreads to neighbors over time
   - Create epidemic simulation on graph!

### 3. **Traffic Flow**
   - Multiple "cars" following paths
   - Cars slow down when near each other
   - Visualize congestion at hubs

### 4. **Particle System**
   - Spawn particles at random nodes
   - Particles follow random walks through graph
   - Create organic, chaotic movement

### 5. **Heat Diffusion**
   - Heat sources at specific nodes
   - Heat spreads to neighbors each frame
   - Visualize thermal flow through network

---

## Summary: The Mental Model

When creating graph-based animations, think:

1. **"What do I want to happen in the NETWORK?"**
   - Not "What should LED 47 do?"
   - But "What should nodes connected to the hub do?"

2. **"How do I traverse/search the graph?"**
   - BFS for layer-by-layer spreading
   - Pathfinding for point-to-point travel
   - Random walk for organic exploration

3. **"How do I render this as colors?"**
   - Distance-based glow for smooth effects
   - Path-based coloring for fills
   - Time-based modulation for animation

4. **"Let the mapping handle the rest!"**
   - Don't worry about physical LED indices
   - Don't worry about strip configuration
   - The mapper translates graph → hardware

**The result:** Complex, beautiful animations with simple, intuitive code!
