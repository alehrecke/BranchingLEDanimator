using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "FountainAnimation", menuName = "LED Animations/Interactive/Fountain")]
    public class FountainAnimation : LEDAnimationType
    {
        [Header("Fountain Settings")]
        [Tooltip("Particles spawned per second")]
        [Range(0.1f, 50f)]
        [SerializeField] private float spawnRate = 5f;
        
        [Tooltip("Maximum particles in the system")]
        [Range(1, 500)]
        [SerializeField] private int maxParticles = 50;
        
        [Tooltip("Base particle travel speed (nodes per second)")]
        [Range(0.1f, 20f)]
        [SerializeField] private float particleSpeed = 3f;
        
        [Tooltip("How strongly particles prefer downward paths (0 = random, 1 = always down)")]
        [Range(0f, 1f)]
        [SerializeField] private float gravityBias = 0.8f;
        
        [Tooltip("Respawn particles at top after reaching endpoint")]
        [SerializeField] private bool continuousFlow = true;
        
        [Header("Visual Settings")]
        [Tooltip("Use rainbow colors for particles")]
        [SerializeField] private bool useRainbowColors = true;
        
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowSaturation = 0.9f;
        
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowBrightness = 1f;
        
        [Tooltip("Particle width in nodes/LEDs")]
        [Range(1f, 8f)]
        [SerializeField] private float particleWidth = 3f;
        
        [Tooltip("Trail length behind particle")]
        [Range(0f, 10f)]
        [SerializeField] private float trailLength = 5f;
        
        [Tooltip("Trail intensity")]
        [Range(0f, 1f)]
        [SerializeField] private float trailIntensity = 0.6f;
        
        [Header("Interaction Settings")]
        [Tooltip("Enable touch interaction to block branches")]
        [SerializeField] private bool enableInteraction = true;
        
        [Tooltip("Glow intensity for blocked branches")]
        [Range(0.5f, 2f)]
        [SerializeField] private float blockGlowIntensity = 1.5f;
        
        [Tooltip("How much blocked branches expand")]
        [Range(1f, 3f)]
        [SerializeField] private float blockExpansion = 1.5f;
        
        [Tooltip("Glow color for blocked branches")]
        [SerializeField] private Color blockGlowColor = new Color(1f, 0.8f, 0.3f, 1f);
        
        [Tooltip("Fade in/out time for block effect")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float blockFadeTime = 0.15f;
        
        [Header("Audio")]
        [SerializeField] private bool enableAudio = false;
        [Range(0f, 1f)]
        [SerializeField] private float audioVolume = 0.3f;
        
        // Particle class
        private class FlowParticle
        {
            public List<int> path;
            public float progress; // 0-1 along current path
            public float hue;
            public int particleIndex;
            public float spawnTime;
            public float speed;
            
            public int CurrentNode => path != null && path.Count > 0 ? 
                path[Mathf.Clamp(Mathf.FloorToInt(progress * (path.Count - 1)), 0, path.Count - 1)] : -1;
            
            public FlowParticle(List<int> nodePath, float time, float assignedHue, int index, float baseSpeed)
            {
                path = nodePath;
                progress = 0f;
                hue = assignedHue;
                particleIndex = index;
                spawnTime = time;
                speed = baseSpeed * Random.Range(0.8f, 1.2f); // Slight variation
            }
        }
        
        // Blocked branch tracking
        private class BlockedBranch
        {
            public int nodeA;
            public int nodeB;
            public float blockStartTime;
            public float intensity; // 0-1 fade
            public bool isBlocked;
            
            public BlockedBranch(int a, int b, float time)
            {
                nodeA = a;
                nodeB = b;
                blockStartTime = time;
                intensity = 0f;
                isBlocked = true;
            }
        }
        
        // State
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private List<Vector3> cachedPositions;
        private List<Vector2Int> cachedEdges;
        private float minHeight, maxHeight;
        private int topNode = -1;
        private bool analysisComplete = false;
        
        private List<FlowParticle> particles = new List<FlowParticle>();
        private Dictionary<(int, int), BlockedBranch> blockedBranches = new Dictionary<(int, int), BlockedBranch>();
        private float lastSpawnTime = 0f;
        private float lastRealTime = 0f;
        private int nextParticleIndex = 0;
        private bool initialized = false;
        
        // Input tracking
        private HashSet<int> touchedNodes = new HashSet<int>();
        private Vector3? lastTouchPosition = null;
        
        // LED mode support
        // Disabled for now - node mode works reliably, LED mapping needs debugging
        public override bool SupportsLEDMode => false;
        private MappedGraph cachedLEDGraph;
        private bool ledMappingInitialized = false;
        
        void OnEnable()
        {
            ledMappingInitialized = false;
            initialized = false;
            analysisComplete = false;
            particles.Clear();
            blockedBranches.Clear();
            nextParticleIndex = 0;
            lastSpawnTime = 0f;
            topNode = -1;
        }
        
        public override Color[] CalculateLEDColors(MappedGraph mappedGraph, float time, int frame)
        {
            if (mappedGraph == null || !mappedGraph.IsValid) return null;
            
            Color[] colors = new Color[mappedGraph.TotalLEDCount];
            
            // Initialize background
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Initialize from LED positions if needed
            if (!initialized && cachedPositions == null)
            {
                cachedPositions = mappedGraph.LEDPositions;
                var edges = new List<Vector2Int>();
                foreach (var edge in mappedGraph.LEDEdges)
                {
                    edges.Add(edge);
                }
                Initialize(Time.realtimeSinceStartup, cachedPositions, edges);
            }
            
            if (!ledMappingInitialized)
            {
                cachedLEDGraph = mappedGraph;
                ledMappingInitialized = true;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(currentTime - lastRealTime, 0.001f, 0.1f);
            lastRealTime = currentTime;
            
            // Update simulation
            UpdateBlockedBranches(deltaTime, currentTime);
            SpawnParticles(currentTime);
            UpdateParticles(deltaTime, currentTime);
            ProcessInput(currentTime);
            
            // Render
            RenderBlockedBranchesLED(colors, mappedGraph, currentTime);
            RenderParticlesLED(colors, mappedGraph);
            
            return colors;
        }
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            if (nodePositions == null || nodePositions.Count == 0)
                return new Color[0];
            
            cachedPositions = nodePositions;
            cachedEdges = edgeConnections;
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize background
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(currentTime - lastRealTime, 0.001f, 0.1f);
            lastRealTime = currentTime;
            
            if (!initialized)
            {
                Initialize(currentTime, nodePositions, edgeConnections);
            }
            
            // Update simulation
            UpdateBlockedBranches(deltaTime, currentTime);
            SpawnParticles(currentTime);
            UpdateParticles(deltaTime, currentTime);
            ProcessInput(currentTime);
            
            // Render
            RenderBlockedBranches(colors, nodePositions, currentTime);
            RenderParticles(colors, nodePositions);
            
            return colors;
        }
        
        private void Initialize(float time, List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            if (nodePositions == null || nodePositions.Count == 0) return;
            
            AnalyzeGraph(nodePositions, edgeConnections);
            
            Debug.Log($"⛲ Fountain: Initialized - top node {topNode}, {endpointNodes.Count} endpoints");
            
            SubscribeToInput();
            
            lastSpawnTime = time;
            initialized = true;
        }
        
        private void AnalyzeGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            if (analysisComplete) return;
            
            cachedPositions = nodePositions;
            cachedEdges = edgeConnections;
            
            // Find height range and top node
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            topNode = 0;
            
            for (int i = 0; i < cachedPositions.Count; i++)
            {
                float y = cachedPositions[i].y;
                minHeight = Mathf.Min(minHeight, y);
                if (y > maxHeight)
                {
                    maxHeight = y;
                    topNode = i;
                }
            }
            
            // Build adjacency
            adjacency.Clear();
            if (edgeConnections != null)
            {
                foreach (var edge in edgeConnections)
                {
                    if (!adjacency.ContainsKey(edge.x))
                        adjacency[edge.x] = new List<int>();
                    if (!adjacency.ContainsKey(edge.y))
                        adjacency[edge.y] = new List<int>();
                    
                    adjacency[edge.x].Add(edge.y);
                    adjacency[edge.y].Add(edge.x);
                }
            }
            
            // Find endpoints
            endpointNodes.Clear();
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    endpointNodes.Add(kvp.Key);
                }
            }
            
            // Find average path length to help calibrate speed
            int samplePathLength = 0;
            if (endpointNodes.Count > 0)
            {
                var samplePath = FindPathBFS(topNode, endpointNodes[0]);
                samplePathLength = samplePath?.Count ?? 0;
            }
            
            Debug.Log($"⛲ Fountain: Graph analyzed - top node {topNode} at height {maxHeight:F2}");
            Debug.Log($"⛲ Fountain: {endpointNodes.Count} drain points, ~{samplePathLength} nodes in typical path");
            Debug.Log($"⛲ Fountain: Height range {minHeight:F2} to {maxHeight:F2} (diff: {maxHeight - minHeight:F2})");
            
            analysisComplete = true;
        }
        
        // ==================== SIMULATION ====================
        
        private void SpawnParticles(float currentTime)
        {
            if (topNode < 0) return;
            
            float spawnInterval = 1f / spawnRate;
            
            while (currentTime - lastSpawnTime >= spawnInterval && particles.Count < maxParticles)
            {
                SpawnParticle(lastSpawnTime + spawnInterval);
                lastSpawnTime += spawnInterval;
            }
        }
        
        private void SpawnParticle(float time)
        {
            // Find a path from top to a random endpoint, respecting blocked branches
            var path = FindFlowPath(topNode);
            
            if (path == null || path.Count < 2)
            {
                // No valid path - all routes blocked? Try again later
                return;
            }
            
            float hue = (nextParticleIndex * 0.618033988749895f) % 1f; // Golden ratio distribution
            var particle = new FlowParticle(path, time, hue, nextParticleIndex, particleSpeed);
            particles.Add(particle);
            nextParticleIndex++;
        }
        
        private List<int> FindFlowPath(int startNode)
        {
            // BFS/gravity-biased search to find path to any endpoint
            var path = new List<int> { startNode };
            var visited = new HashSet<int> { startNode };
            int current = startNode;
            
            int maxSteps = cachedPositions?.Count ?? 1000;
            
            for (int step = 0; step < maxSteps; step++)
            {
                if (!adjacency.ContainsKey(current)) break;
                
                // Check if we reached an endpoint
                if (endpointNodes.Contains(current) && current != startNode)
                {
                    return path;
                }
                
                // Get available neighbors (not visited, not blocked)
                var neighbors = adjacency[current]
                    .Where(n => !visited.Contains(n) && !IsBranchBlocked(current, n))
                    .ToList();
                
                if (neighbors.Count == 0)
                {
                    // Dead end - try to find alternate route
                    // For now, just end here
                    break;
                }
                
                // Choose next node based on gravity bias
                int next = ChooseNextNode(current, neighbors);
                
                visited.Add(next);
                path.Add(next);
                current = next;
            }
            
            // Return path even if we didn't reach an endpoint
            return path.Count > 1 ? path : null;
        }
        
        private int ChooseNextNode(int current, List<int> candidates)
        {
            if (candidates.Count == 1) return candidates[0];
            if (cachedPositions == null) return candidates[Random.Range(0, candidates.Count)];
            
            float currentY = cachedPositions[current].y;
            float heightRange = maxHeight - minHeight;
            if (heightRange < 0.01f) heightRange = 1f;
            
            // Calculate weights based on how much lower each candidate is
            var weights = new List<float>();
            float totalWeight = 0f;
            
            foreach (int candidate in candidates)
            {
                float candidateY = cachedPositions[candidate].y;
                float heightDiff = currentY - candidateY; // Positive = going down
                float normalizedDiff = heightDiff / heightRange; // Normalize to 0-1 range
                
                // Base weight
                float weight = 0.1f;
                
                if (normalizedDiff > 0)
                {
                    // Going down - strongly prefer based on gravity bias
                    weight += normalizedDiff * gravityBias * 20f + gravityBias * 2f;
                }
                else
                {
                    // Going up - penalize based on gravity bias
                    float penalty = Mathf.Lerp(1f, 0.01f, gravityBias);
                    weight *= penalty;
                }
                
                weights.Add(Mathf.Max(0.001f, weight));
                totalWeight += weights[weights.Count - 1];
            }
            
            // Weighted random selection
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return candidates[i];
                }
            }
            
            return candidates[candidates.Count - 1];
        }
        
        /// <summary>
        /// Simple BFS pathfinding (no gravity bias) for measuring path lengths
        /// </summary>
        private List<int> FindPathBFS(int start, int end)
        {
            if (start == end) return new List<int> { start };
            
            var queue = new Queue<List<int>>();
            var visited = new HashSet<int>();
            
            queue.Enqueue(new List<int> { start });
            visited.Add(start);
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                int current = path[path.Count - 1];
                
                if (!adjacency.ContainsKey(current)) continue;
                
                foreach (int neighbor in adjacency[current])
                {
                    if (visited.Contains(neighbor)) continue;
                    
                    var newPath = new List<int>(path) { neighbor };
                    
                    if (neighbor == end)
                        return newPath;
                    
                    visited.Add(neighbor);
                    queue.Enqueue(newPath);
                }
            }
            
            return null;
        }
        
        private void UpdateParticles(float deltaTime, float currentTime)
        {
            var particlesToRemove = new List<FlowParticle>();
            
            foreach (var particle in particles)
            {
                if (particle.path == null || particle.path.Count < 2) continue;
                
                // Move particle along path
                // Speed is in "nodes per second" - consistent speed regardless of path length
                float pathSegments = particle.path.Count - 1;
                if (pathSegments < 1) pathSegments = 1;
                float progressDelta = (particle.speed * deltaTime) / pathSegments;
                particle.progress += progressDelta;
                
                // Check if particle reached end of path
                if (particle.progress >= 1f)
                {
                    int endNode = particle.path[particle.path.Count - 1];
                    
                    if (endpointNodes.Contains(endNode))
                    {
                        // Reached drain point
                        if (continuousFlow)
                        {
                            // Respawn at top
                            var newPath = FindFlowPath(topNode);
                            if (newPath != null && newPath.Count > 1)
                            {
                                particle.path = newPath;
                                particle.progress = 0f;
                                particle.spawnTime = currentTime;
                            }
                            else
                            {
                                particlesToRemove.Add(particle);
                            }
                        }
                        else
                        {
                            particlesToRemove.Add(particle);
                        }
                    }
                    else
                    {
                        // Hit a dead end (blocked path?) - try to continue
                        var continuePath = FindFlowPath(endNode);
                        if (continuePath != null && continuePath.Count > 1)
                        {
                            particle.path = continuePath;
                            particle.progress = 0f;
                        }
                        else
                        {
                            particlesToRemove.Add(particle);
                        }
                    }
                }
            }
            
            foreach (var p in particlesToRemove)
            {
                particles.Remove(p);
            }
        }
        
        // ==================== BLOCKING ====================
        
        private bool IsBranchBlocked(int nodeA, int nodeB)
        {
            var key = GetBranchKey(nodeA, nodeB);
            return blockedBranches.TryGetValue(key, out var branch) && branch.isBlocked && branch.intensity > 0.1f;
        }
        
        private (int, int) GetBranchKey(int a, int b)
        {
            return a < b ? (a, b) : (b, a);
        }
        
        private void BlockBranch(int nodeA, int nodeB, float time)
        {
            var key = GetBranchKey(nodeA, nodeB);
            
            if (!blockedBranches.ContainsKey(key))
            {
                blockedBranches[key] = new BlockedBranch(nodeA, nodeB, time);
            }
            else
            {
                blockedBranches[key].isBlocked = true;
                blockedBranches[key].blockStartTime = time;
            }
        }
        
        private void UnblockBranch(int nodeA, int nodeB)
        {
            var key = GetBranchKey(nodeA, nodeB);
            if (blockedBranches.ContainsKey(key))
            {
                blockedBranches[key].isBlocked = false;
            }
        }
        
        private void UpdateBlockedBranches(float deltaTime, float currentTime)
        {
            var toRemove = new List<(int, int)>();
            
            foreach (var kvp in blockedBranches)
            {
                var branch = kvp.Value;
                
                if (branch.isBlocked)
                {
                    // Fade in
                    branch.intensity = Mathf.MoveTowards(branch.intensity, 1f, deltaTime / blockFadeTime);
                }
                else
                {
                    // Fade out
                    branch.intensity = Mathf.MoveTowards(branch.intensity, 0f, deltaTime / blockFadeTime);
                    
                    if (branch.intensity <= 0f)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }
            
            foreach (var key in toRemove)
            {
                blockedBranches.Remove(key);
            }
        }
        
        // ==================== INPUT ====================
        
        private void SubscribeToInput()
        {
            if (enableInteraction)
            {
                GraphPlayerController.OnEndpointPressed -= OnNodePressed;
                GraphPlayerController.OnEndpointPressed += OnNodePressed;
                GraphPlayerController.OnEndpointReleased -= OnNodeReleased;
                GraphPlayerController.OnEndpointReleased += OnNodeReleased;
            }
        }
        
        private void OnNodePressed(int nodeIndex, Vector3 position)
        {
            if (!enableInteraction) return;
            
            touchedNodes.Add(nodeIndex);
            lastTouchPosition = position;
            
            // Block all branches connected to this node
            if (adjacency.ContainsKey(nodeIndex))
            {
                float currentTime = Time.realtimeSinceStartup;
                foreach (int neighbor in adjacency[nodeIndex])
                {
                    BlockBranch(nodeIndex, neighbor, currentTime);
                }
            }
        }
        
        private void OnNodeReleased(int nodeIndex, Vector3 position)
        {
            touchedNodes.Remove(nodeIndex);
            
            // Unblock branches connected to this node
            if (adjacency.ContainsKey(nodeIndex))
            {
                foreach (int neighbor in adjacency[nodeIndex])
                {
                    // Only unblock if the other end isn't also touched
                    if (!touchedNodes.Contains(neighbor))
                    {
                        UnblockBranch(nodeIndex, neighbor);
                    }
                }
            }
        }
        
        private void ProcessInput(float currentTime)
        {
            // Handle mouse/touch input for blocking branches
            if (!enableInteraction) return;
            
            if (Input.GetMouseButton(0) && cachedPositions != null)
            {
                // Find closest node to click
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    int closestNode = FindClosestNodeToRay(ray);
                    
                    if (closestNode >= 0 && !touchedNodes.Contains(closestNode))
                    {
                        // Simulate press
                        OnNodePressed(closestNode, cachedPositions[closestNode]);
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                // Release all touched nodes
                var nodesToRelease = touchedNodes.ToList();
                foreach (int node in nodesToRelease)
                {
                    OnNodeReleased(node, cachedPositions != null && node < cachedPositions.Count ? 
                        cachedPositions[node] : Vector3.zero);
                }
            }
        }
        
        private int FindClosestNodeToRay(Ray ray)
        {
            if (cachedPositions == null) return -1;
            
            int closest = -1;
            float closestDist = 2f; // Max distance threshold
            
            for (int i = 0; i < cachedPositions.Count; i++)
            {
                Vector3 nodePos = cachedPositions[i];
                float dist = Vector3.Cross(ray.direction, nodePos - ray.origin).magnitude;
                
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = i;
                }
            }
            
            return closest;
        }
        
        // ==================== NODE MODE RENDERING ====================
        
        private void RenderBlockedBranches(Color[] colors, List<Vector3> nodePositions, float time)
        {
            foreach (var kvp in blockedBranches)
            {
                var branch = kvp.Value;
                if (branch.intensity <= 0f) continue;
                
                int nodeA = branch.nodeA;
                int nodeB = branch.nodeB;
                
                if (nodeA >= colors.Length || nodeB >= colors.Length) continue;
                
                // Find all nodes along this branch
                var branchNodes = GetBranchNodes(nodeA, nodeB);
                
                float effectiveWidth = particleWidth * blockExpansion * branch.intensity;
                float pulse = 0.9f + 0.1f * Mathf.Sin(time * 3f);
                Color glowColor = blockGlowColor * blockGlowIntensity * branch.intensity * pulse;
                
                foreach (int node in branchNodes)
                {
                    if (node >= colors.Length) continue;
                    colors[node] = BlendMax(colors[node], glowColor, branch.intensity);
                }
            }
        }
        
        private List<int> GetBranchNodes(int nodeA, int nodeB)
        {
            // Get all nodes along a branch between two junction points
            var nodes = new List<int> { nodeA };
            
            // Simple: just return the two nodes for now
            // Could expand to traverse the full branch
            nodes.Add(nodeB);
            
            return nodes;
        }
        
        private void RenderParticles(Color[] colors, List<Vector3> nodePositions)
        {
            foreach (var particle in particles)
            {
                if (particle.path == null || particle.path.Count < 2) continue;
                
                int pathLength = particle.path.Count;
                float position = particle.progress * (pathLength - 1);
                
                Color particleColor = useRainbowColors
                    ? Color.HSVToRGB(particle.hue, rainbowSaturation, rainbowBrightness)
                    : primaryColor;
                
                // Render particle head
                for (int i = 0; i < pathLength; i++)
                {
                    float dist = Mathf.Abs(i - position);
                    
                    if (dist <= particleWidth)
                    {
                        int nodeIndex = particle.path[i];
                        if (nodeIndex >= colors.Length) continue;
                        
                        float intensity = 1f - (dist / particleWidth);
                        intensity = Mathf.Pow(intensity, 0.5f); // Softer falloff
                        
                        colors[nodeIndex] = BlendMax(colors[nodeIndex], particleColor, intensity);
                    }
                }
                
                // Render trail
                if (trailLength > 0 && trailIntensity > 0)
                {
                    for (int i = 0; i < pathLength; i++)
                    {
                        float signedDist = i - position;
                        
                        // Trail is behind the particle (negative distance)
                        if (signedDist < 0 && signedDist > -trailLength)
                        {
                            int nodeIndex = particle.path[i];
                            if (nodeIndex >= colors.Length) continue;
                            
                            float trailPos = -signedDist / trailLength;
                            float fade = (1f - trailPos) * trailIntensity;
                            
                            Color trailColor = useRainbowColors
                                ? Color.HSVToRGB(particle.hue, rainbowSaturation * (1f - trailPos * 0.3f), rainbowBrightness * (1f - trailPos * 0.5f))
                                : Color.Lerp(primaryColor, secondaryColor, trailPos);
                            
                            colors[nodeIndex] = BlendMax(colors[nodeIndex], trailColor, fade);
                        }
                    }
                }
            }
        }
        
        // ==================== LED MODE RENDERING ====================
        
        private void RenderBlockedBranchesLED(Color[] colors, MappedGraph mappedGraph, float time)
        {
            foreach (var kvp in blockedBranches)
            {
                var branch = kvp.Value;
                if (branch.intensity <= 0f) continue;
                
                // For LED mode, find LEDs near the blocked branch nodes
                int nodeA = branch.nodeA;
                int nodeB = branch.nodeB;
                
                float pulse = 0.9f + 0.1f * Mathf.Sin(time * 3f);
                Color glowColor = blockGlowColor * blockGlowIntensity * branch.intensity * pulse;
                
                // Light up LEDs near these nodes
                for (int led = 0; led < mappedGraph.TotalLEDCount && led < mappedGraph.LEDPositions.Count; led++)
                {
                    Vector3 ledPos = mappedGraph.LEDPositions[led];
                    
                    float distA = nodeA < cachedPositions.Count ? 
                        Vector3.Distance(ledPos, cachedPositions[nodeA]) : float.MaxValue;
                    float distB = nodeB < cachedPositions.Count ? 
                        Vector3.Distance(ledPos, cachedPositions[nodeB]) : float.MaxValue;
                    
                    float minDist = Mathf.Min(distA, distB);
                    float threshold = 0.5f * blockExpansion;
                    
                    if (minDist < threshold)
                    {
                        float intensity = (1f - minDist / threshold) * branch.intensity;
                        colors[led] = BlendMax(colors[led], glowColor, intensity);
                    }
                }
            }
        }
        
        private void RenderParticlesLED(Color[] colors, MappedGraph mappedGraph)
        {
            foreach (var particle in particles)
            {
                if (particle.path == null || particle.path.Count < 2) continue;
                
                int pathLength = particle.path.Count;
                float position = particle.progress * (pathLength - 1);
                
                // Get interpolated position along path
                int pathIndexA = Mathf.FloorToInt(position);
                int pathIndexB = Mathf.CeilToInt(position);
                pathIndexA = Mathf.Clamp(pathIndexA, 0, pathLength - 1);
                pathIndexB = Mathf.Clamp(pathIndexB, 0, pathLength - 1);
                
                int nodeA = particle.path[pathIndexA];
                int nodeB = particle.path[pathIndexB];
                
                if (nodeA >= cachedPositions.Count || nodeB >= cachedPositions.Count) continue;
                
                float lerp = position - pathIndexA;
                Vector3 particlePos = Vector3.Lerp(cachedPositions[nodeA], cachedPositions[nodeB], lerp);
                
                Color particleColor = useRainbowColors
                    ? Color.HSVToRGB(particle.hue, rainbowSaturation, rainbowBrightness)
                    : primaryColor;
                
                // Render particle on nearby LEDs
                for (int led = 0; led < mappedGraph.TotalLEDCount && led < mappedGraph.LEDPositions.Count; led++)
                {
                    Vector3 ledPos = mappedGraph.LEDPositions[led];
                    float dist = Vector3.Distance(ledPos, particlePos);
                    
                    // Particle head
                    float headRadius = particleWidth * 0.1f; // Scale for world units
                    if (dist < headRadius)
                    {
                        float intensity = 1f - (dist / headRadius);
                        intensity = Mathf.Pow(intensity, 0.5f);
                        colors[led] = BlendMax(colors[led], particleColor, intensity);
                    }
                    
                    // Trail - check distance to path behind particle
                    if (trailLength > 0 && trailIntensity > 0)
                    {
                        for (int pi = 0; pi < pathIndexA && pi < pathLength; pi++)
                        {
                            int trailNode = particle.path[pi];
                            if (trailNode >= cachedPositions.Count) continue;
                            
                            float trailDist = Vector3.Distance(ledPos, cachedPositions[trailNode]);
                            float trailRadius = headRadius * 0.7f;
                            
                            if (trailDist < trailRadius)
                            {
                                float pathDist = pathIndexA - pi;
                                float trailFade = Mathf.Clamp01(1f - pathDist / trailLength);
                                float spatialFade = 1f - (trailDist / trailRadius);
                                float totalFade = trailFade * spatialFade * trailIntensity;
                                
                                Color trailColor = useRainbowColors
                                    ? Color.HSVToRGB(particle.hue, rainbowSaturation * 0.8f, rainbowBrightness * trailFade)
                                    : Color.Lerp(secondaryColor, primaryColor, trailFade);
                                
                                colors[led] = BlendMax(colors[led], trailColor, totalFade);
                            }
                        }
                    }
                }
            }
        }
        
        // ==================== UTILITIES ====================
        
        private Color BlendMax(Color current, Color add, float intensity)
        {
            Color blended = add * intensity;
            return new Color(
                Mathf.Max(current.r, blended.r),
                Mathf.Max(current.g, blended.g),
                Mathf.Max(current.b, blended.b),
                Mathf.Max(current.a, blended.a)
            );
        }
        
        // ==================== PUBLIC API ====================
        
        public int ParticleCount => particles.Count;
        public int BlockedBranchCount => blockedBranches.Count(b => b.Value.isBlocked);
        
        public void ClearAllBlocks()
        {
            foreach (var branch in blockedBranches.Values)
            {
                branch.isBlocked = false;
            }
            touchedNodes.Clear();
        }
        
        public void ResetAnimation()
        {
            particles.Clear();
            blockedBranches.Clear();
            touchedNodes.Clear();
            nextParticleIndex = 0;
            lastSpawnTime = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// Manually block a branch (for external control/simulation)
        /// </summary>
        public void SimulateBlockBranch(int nodeIndex)
        {
            if (adjacency.ContainsKey(nodeIndex))
            {
                float currentTime = Time.realtimeSinceStartup;
                foreach (int neighbor in adjacency[nodeIndex])
                {
                    BlockBranch(nodeIndex, neighbor, currentTime);
                }
                touchedNodes.Add(nodeIndex);
            }
        }
        
        /// <summary>
        /// Manually unblock a branch
        /// </summary>
        public void SimulateUnblockBranch(int nodeIndex)
        {
            if (adjacency.ContainsKey(nodeIndex))
            {
                foreach (int neighbor in adjacency[nodeIndex])
                {
                    if (!touchedNodes.Contains(neighbor))
                    {
                        UnblockBranch(nodeIndex, neighbor);
                    }
                }
                touchedNodes.Remove(nodeIndex);
            }
        }
    }
}
