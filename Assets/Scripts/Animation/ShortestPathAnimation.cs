using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Progressive shortest path animation with overwriting effect
    /// Each new path overwrites colors of previous paths it crosses
    /// </summary>
    [CreateAssetMenu(fileName = "ShortestPathAnimation", menuName = "LED Animations/Shortest Path Animation")]
    public class ShortestPathAnimation : LEDAnimationType
    {
        [Header("Path Finding")]
        [SerializeField] private PathMode pathMode = PathMode.AllPairsShortestPaths;
        
        [Header("Visualization")]
        [SerializeField] private Color startNodeColor = Color.red;
        [SerializeField] private Color endNodeColor = Color.blue;
        [SerializeField] private Color exploredColor = Color.yellow;
        [SerializeField] private AnimationCurve pathIntensity = AnimationCurve.EaseInOut(0, 1, 1, 0);
        
        [Header("Path Generation")]
        [SerializeField] private bool showExploration = false;
        [SerializeField] private int maxConcurrentPaths = 3;
        [SerializeField] private float pathInterval = 3f;
        
        [Header("Network Painting Mode")]
        [SerializeField] private bool networkPaintingMode = true; // Progressive network painting with overwriting
        [SerializeField] private bool useColorWheel = true; // Progress through color spectrum
        [SerializeField] private float fillSpeed = 2f; // How fast paths fill
        [SerializeField] private bool clearNetworkOnLoop = true; // Clear painted network when animation loops
        
        [Header("Legacy Path Options")]
        [SerializeField] private bool legacyTravelingWave = false; // Old traveling wave mode
        [SerializeField] private bool legacyPersistentPaths = false; // Old persistent path system
        [SerializeField] private Gradient pathColorGradient; // For legacy gradient mode
        [SerializeField] private float pathFadeTime = 5f; // For legacy fading
        
        public enum PathMode
        {
            AllPairsShortestPaths,  // Random start/end pairs
            RadialFromCenter,       // Paths radiating from central nodes
            EndpointConnections,    // Connect all endpoints
            FloodFill               // Expanding flood fill from sources
        }
        
        [System.Serializable]
        private class PathTraversal
        {
            public List<int> fullPath;
            public int currentNodeIndex;
            public float progress;              // 0-1 along current edge
            public float totalProgress;        // 0-1 along entire path
            public float startTime;
            public Color color;
            public bool isComplete;
            public List<int> exploredNodes;    // For algorithm visualization
            public int pathId;                  // Unique identifier for this path
            public float completionTime;       // When this path was completed
            
            public PathTraversal(List<int> path, Color col, float time, int id)
            {
                fullPath = new List<int>(path);
                currentNodeIndex = 0;
                progress = 0f;
                totalProgress = 0f;
                startTime = time;
                color = col;
                isComplete = false;
                exploredNodes = new List<int>();
                pathId = id;
                completionTime = -1f;
            }
        }
        
        // Graph structure cache
        private Dictionary<int, List<int>> nodeConnections = new Dictionary<int, List<int>>();
        private Dictionary<int, int> nodeValences = new Dictionary<int, int>();
        private Dictionary<(int, int), float> edgeWeights = new Dictionary<(int, int), float>();
        private List<int> centralNodes = new List<int>();
        private List<int> endpointNodes = new List<int>();
        private bool analysisComplete = false;
        
        // Path state
        private List<PathTraversal> activeTraversals = new List<PathTraversal>();
        private List<PathTraversal> completedPaths = new List<PathTraversal>(); // For legacy persistent paths
        private Dictionary<int, PathTraversal> nodeToLatestPath = new Dictionary<int, PathTraversal>(); // Track which path owns each node
        private float lastPathTime = 0f;
        private float lastAnimTime = 0f; // Track time resets for looping
        private int nextPathId = 0; // For unique path identification
        
        // Network painting state
        private Dictionary<int, Color> paintedNetwork = new Dictionary<int, Color>(); // Current painted state of network
        private float currentHue = 0f; // Current position on color wheel
        
        // Pathfinding cache
        private Dictionary<(int, int), List<int>> shortestPathsCache = new Dictionary<(int, int), List<int>>();
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Perform graph analysis if needed
            if (!analysisComplete)
            {
                AnalyzeGraphStructure(nodePositions, edgeConnections);
            }
            
            // Highlight special nodes
            ApplyNodeRoleColors(colors);
            
            // Handle looping time
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
                
                // Detect loop reset and clear paths for fresh start
                if (animTime < lastAnimTime)
                {
                    Debug.Log("🔄 Shortest Path: Loop reset detected - clearing paths");
                    activeTraversals.Clear();
                    completedPaths.Clear();
                    nodeToLatestPath.Clear();
                    lastPathTime = 0f;
                    nextPathId = 0;
                    
                    // Reset network painting if enabled
                    if (networkPaintingMode && clearNetworkOnLoop)
                    {
                        paintedNetwork.Clear();
                        currentHue = 0f;
                    }
                }
            }
            lastAnimTime = animTime;
            
            // Generate and update paths
            UpdatePathSystem(animTime, nodePositions);
            
            // Apply path effects based on mode
            if (networkPaintingMode)
            {
                ApplyNetworkPaintingEffects(colors, animTime);
            }
            else
            {
                ApplyLegacyPathEffects(colors, animTime);
            }
            
            return colors;
        }
        
        void AnalyzeGraphStructure(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Clear previous analysis
            nodeConnections.Clear();
            nodeValences.Clear();
            edgeWeights.Clear();
            centralNodes.Clear();
            endpointNodes.Clear();
            shortestPathsCache.Clear();
            
            // Initialize structures
            for (int i = 0; i < nodePositions.Count; i++)
            {
                nodeConnections[i] = new List<int>();
                nodeValences[i] = 0;
            }
            
            // Build connection graph
            foreach (var edge in edgeConnections)
            {
                int nodeA = edge.x;
                int nodeB = edge.y;
                
                if (nodeA < nodePositions.Count && nodeB < nodePositions.Count)
                {
                    // Add bidirectional connections
                    if (!nodeConnections[nodeA].Contains(nodeB))
                    {
                        nodeConnections[nodeA].Add(nodeB);
                        nodeValences[nodeA]++;
                    }
                    
                    if (!nodeConnections[nodeB].Contains(nodeA))
                    {
                        nodeConnections[nodeB].Add(nodeA);
                        nodeValences[nodeB]++;
                    }
                    
                    // Calculate edge weights (Euclidean distance)
                    float weight = Vector3.Distance(nodePositions[nodeA], nodePositions[nodeB]);
                    edgeWeights[(nodeA, nodeB)] = weight;
                    edgeWeights[(nodeB, nodeA)] = weight;
                }
            }
            
            // Identify central and endpoint nodes
            foreach (var kvp in nodeValences)
            {
                int nodeIndex = kvp.Key;
                int valence = kvp.Value;
                
                if (valence >= 3)
                {
                    centralNodes.Add(nodeIndex);
                }
                else if (valence == 1)
                {
                    endpointNodes.Add(nodeIndex);
                }
            }
            
            analysisComplete = true;
        }
        
        void ApplyNodeRoleColors(Color[] colors)
        {
            foreach (int central in centralNodes)
            {
                if (central < colors.Length)
                    colors[central] = Color.Lerp(inactiveColor, startNodeColor, 0.2f);
            }
            
            foreach (int endpoint in endpointNodes)
            {
                if (endpoint < colors.Length)
                    colors[endpoint] = Color.Lerp(inactiveColor, endNodeColor, 0.2f);
            }
        }
        
        void UpdatePathSystem(float animTime, List<Vector3> nodePositions)
        {
            // Generate new paths
            if (animTime - lastPathTime >= pathInterval && activeTraversals.Count < maxConcurrentPaths)
            {
                GenerateNewPath(animTime);
                lastPathTime = animTime;
            }
            
            // Update existing path traversals
            UpdatePathTraversals(animTime);
        }
        
        void GenerateNewPath(float animTime)
        {
            var pathPair = GetPathStartEnd();
            if (pathPair.start == -1 || pathPair.end == -1) return;
            
            // Get color for this path
            Color pathColor = GetPathColorFromGradient(nextPathId);
            
            // Create traversal first so we can populate exploration data
            var traversal = new PathTraversal(new List<int>(), pathColor, animTime, nextPathId);
            nextPathId++;
            
            // Find path and populate exploration data
            List<int> path = FindShortestPathWithExploration(pathPair.start, pathPair.end, traversal);
            if (path.Count > 1)
            {
                traversal.fullPath = path;
                activeTraversals.Add(traversal);
            }
        }
        
        Color GetPathColorFromGradient(int pathId)
        {
            if (networkPaintingMode && useColorWheel)
            {
                // Network painting mode: use color wheel progression
                Color pathColor = Color.HSVToRGB(currentHue, 0.8f, 1f);
                
                // Advance hue for next path (golden ratio for pleasing distribution)
                currentHue += 0.618f; // Golden ratio
                if (currentHue > 1f) currentHue -= 1f;
                
                return pathColor;
            }
            else if (pathColorGradient != null)
            {
                // Legacy gradient mode
                float gradientTime = (pathId % 10) / 10f; // Cycle through gradient every 10 paths
                return pathColorGradient.Evaluate(gradientTime);
            }
            else
            {
                // Fallback to original color logic
                return primaryColor;
            }
        }
        
        (int start, int end) GetPathStartEnd()
        {
            switch (pathMode)
            {
                case PathMode.AllPairsShortestPaths:
                    // Random pairs from all nodes
                    var allNodes = nodeConnections.Keys.ToList();
                    if (allNodes.Count < 2) return (-1, -1);
                    int start = allNodes[Random.Range(0, allNodes.Count)];
                    int end = allNodes[Random.Range(0, allNodes.Count)];
                    while (end == start && allNodes.Count > 1)
                    {
                        end = allNodes[Random.Range(0, allNodes.Count)];
                    }
                    return (start, end);
                    
                case PathMode.RadialFromCenter:
                    // From central nodes to endpoints
                    if (centralNodes.Count == 0 || endpointNodes.Count == 0) return (-1, -1);
                    return (centralNodes[Random.Range(0, centralNodes.Count)], 
                           endpointNodes[Random.Range(0, endpointNodes.Count)]);
                    
                case PathMode.EndpointConnections:
                    // Between endpoints
                    if (endpointNodes.Count < 2) return (-1, -1);
                    int ep1 = endpointNodes[Random.Range(0, endpointNodes.Count)];
                    int ep2 = endpointNodes[Random.Range(0, endpointNodes.Count)];
                    while (ep2 == ep1 && endpointNodes.Count > 1)
                    {
                        ep2 = endpointNodes[Random.Range(0, endpointNodes.Count)];
                    }
                    return (ep1, ep2);
                    
                case PathMode.FloodFill:
                    // From central nodes outward
                    if (centralNodes.Count == 0) return (-1, -1);
                    var allReachable = nodeConnections.Keys.ToList();
                    int source = centralNodes[Random.Range(0, centralNodes.Count)];
                    int target = allReachable[Random.Range(0, allReachable.Count)];
                    return (source, target);
                    
                default:
                    return (-1, -1);
            }
        }
        
        Color GetPathColor(int startNode, int endNode)
        {
            if (centralNodes.Contains(startNode) && endpointNodes.Contains(endNode))
                return Color.Lerp(primaryColor, startNodeColor, 0.3f);
            else if (endpointNodes.Contains(startNode) && endpointNodes.Contains(endNode))
                return Color.Lerp(primaryColor, endNodeColor, 0.3f);
            else
                return primaryColor;
        }
        
        List<int> FindShortestPath(int start, int end)
        {
            // Check cache first
            if (shortestPathsCache.ContainsKey((start, end)))
            {
                return new List<int>(shortestPathsCache[(start, end)]);
            }
            
            return FindShortestPathWithExploration(start, end, null);
        }
        
        List<int> FindShortestPathWithExploration(int start, int end, PathTraversal traversal)
        {
            // Check cache first
            if (shortestPathsCache.ContainsKey((start, end)))
            {
                return new List<int>(shortestPathsCache[(start, end)]);
            }
            
            // Dijkstra's algorithm
            var distances = new Dictionary<int, float>();
            var previous = new Dictionary<int, int>();
            var unvisited = new HashSet<int>(nodeConnections.Keys);
            var explored = new List<int>(); // Track exploration for visualization
            
            // Initialize distances
            foreach (int node in nodeConnections.Keys)
            {
                distances[node] = float.MaxValue;
            }
            distances[start] = 0f;
            
            while (unvisited.Count > 0)
            {
                // Find unvisited node with minimum distance
                int current = unvisited.OrderBy(n => distances[n]).First();
                unvisited.Remove(current);
                explored.Add(current); // Track exploration
                
                // Early exit if we reached the target
                if (current == end) break;
                
                // Check neighbors
                if (nodeConnections.ContainsKey(current))
                {
                    foreach (int neighbor in nodeConnections[current])
                    {
                        if (!unvisited.Contains(neighbor)) continue;
                        
                        float edgeWeight = edgeWeights.ContainsKey((current, neighbor)) ? 
                                         edgeWeights[(current, neighbor)] : 1f;
                        float altDistance = distances[current] + edgeWeight;
                        
                        if (altDistance < distances[neighbor])
                        {
                            distances[neighbor] = altDistance;
                            previous[neighbor] = current;
                        }
                    }
                }
            }
            
            // Reconstruct path
            var path = new List<int>();
            int pathNode = end;
            
            while (previous.ContainsKey(pathNode))
            {
                path.Add(pathNode);
                pathNode = previous[pathNode];
            }
            
            if (pathNode == start)
            {
                path.Add(start);
                path.Reverse();
                
                // Cache the result
                shortestPathsCache[(start, end)] = new List<int>(path);
                
                // Store exploration data with the traversal (for show exploration feature)
                if (showExploration && traversal != null)
                {
                    traversal.exploredNodes = explored;
                }
                
                return path;
            }
            
            // No path found
            return new List<int>();
        }
        
        void UpdatePathTraversals(float animTime)
        {
            for (int i = activeTraversals.Count - 1; i >= 0; i--)
            {
                var traversal = activeTraversals[i];
                
                if (traversal.isComplete)
                {
                    if (legacyPersistentPaths)
                    {
                        // Move to completed paths list
                        if (!completedPaths.Contains(traversal))
                        {
                            traversal.completionTime = animTime;
                            completedPaths.Add(traversal);
                            
                            // Update node ownership for overwriting (always true in network painting mode)
                            foreach (int node in traversal.fullPath)
                            {
                                nodeToLatestPath[node] = traversal;
                            }
                        }
                    }
                    
                    // Remove from active list
                    activeTraversals.RemoveAt(i);
                    continue;
                }
                
                // Update progress along path
                float effectiveSpeed;
                float pathDuration;
                
                if (networkPaintingMode)
                {
                    // Network painting mode: always use fill behavior
                    effectiveSpeed = speed * fillSpeed;
                    pathDuration = traversal.fullPath.Count * 0.3f / effectiveSpeed;
                }
                else if (legacyTravelingWave)
                {
                    // Legacy traveling wave mode
                    effectiveSpeed = speed * 2f;
                    pathDuration = traversal.fullPath.Count * 0.5f / effectiveSpeed;
                }
                else
                {
                    // Legacy fill mode
                    effectiveSpeed = speed * fillSpeed;
                    pathDuration = traversal.fullPath.Count * 0.3f / effectiveSpeed;
                }
                
                float timeElapsed = animTime - traversal.startTime;
                traversal.totalProgress = Mathf.Clamp01(timeElapsed / pathDuration);
                
                if (traversal.totalProgress >= 1f)
                {
                    traversal.isComplete = true;
                    traversal.currentNodeIndex = traversal.fullPath.Count - 1;
                }
                else
                {
                    // Calculate current node and progress along current edge
                    float pathPosition = traversal.totalProgress * (traversal.fullPath.Count - 1);
                    traversal.currentNodeIndex = Mathf.FloorToInt(pathPosition);
                    traversal.progress = pathPosition - traversal.currentNodeIndex;
                }
            }
        }
        
        void ApplyNetworkPaintingEffects(Color[] colors, float animTime)
        {
            // Apply painted network as base layer
            foreach (var kvp in paintedNetwork)
            {
                int nodeIndex = kvp.Key;
                Color nodeColor = kvp.Value;
                
                if (nodeIndex < colors.Length)
                {
                    colors[nodeIndex] = Color.Lerp(colors[nodeIndex], nodeColor, 0.8f);
                }
            }
            
            // Apply active filling paths on top
            foreach (var traversal in activeTraversals)
            {
                ApplyNetworkPaintingPath(traversal, colors, animTime);
            }
        }
        
        void ApplyNetworkPaintingPath(PathTraversal traversal, Color[] colors, float animTime)
        {
            if (traversal.fullPath.Count == 0) return;
            
            // Show exploration effect if enabled
            if (showExploration && !traversal.isComplete)
            {
                foreach (int explored in traversal.exploredNodes)
                {
                    if (explored < colors.Length)
                    {
                        colors[explored] = Color.Lerp(colors[explored], exploredColor, 0.3f);
                    }
                }
            }
            
            // Apply filling path effect
            for (int i = 0; i < traversal.fullPath.Count; i++)
            {
                int nodeIndex = traversal.fullPath[i];
                if (nodeIndex >= colors.Length) continue;
                
                float nodeProgress = (float)i / (traversal.fullPath.Count - 1);
                
                if (traversal.isComplete)
                {
                    // Path is complete - paint it to the network permanently
                    paintedNetwork[nodeIndex] = traversal.color;
                    colors[nodeIndex] = traversal.color;
                }
                else if (nodeProgress <= traversal.totalProgress)
                {
                    // Path is actively filling - show bright fill effect
                    float intensity = 1f;
                    
                    // Extra brightness at fill front
                    float distanceFromFront = traversal.totalProgress - nodeProgress;
                    if (distanceFromFront < 0.1f)
                    {
                        intensity = 1.2f; // Extra bright at the front
                    }
                    
                    Color fillColor = Color.Lerp(traversal.color, Color.white, 0.2f); // Slightly brighter while filling
                    colors[nodeIndex] = Color.Lerp(colors[nodeIndex], fillColor, intensity);
                }
            }
        }
        
        void ApplyLegacyPathEffects(Color[] colors, float animTime)
        {
            // Apply persistent paths first (background layer)
            if (legacyPersistentPaths)
            {
                ApplyPersistentPathEffects(colors, animTime);
            }
            
            // Apply active traversals on top
            foreach (var traversal in activeTraversals)
            {
                ApplyPathEffect(traversal, colors, false);
            }
        }
        
        void ApplyPersistentPathEffects(Color[] colors, float animTime)
        {
            foreach (var completedPath in completedPaths)
            {
                // Calculate fade based on age
                float age = animTime - completedPath.completionTime;
                float fadeAlpha = 1f;
                
                if (pathFadeTime > 0 && age > pathFadeTime)
                {
                    fadeAlpha = Mathf.Max(0.1f, 1f - ((age - pathFadeTime) / pathFadeTime));
                }
                
                ApplyPersistentPathEffect(completedPath, colors, fadeAlpha);
            }
        }
        
        void ApplyPersistentPathEffect(PathTraversal traversal, Color[] colors, float fadeAlpha)
        {
            if (traversal.fullPath.Count == 0) return;
            
            for (int i = 0; i < traversal.fullPath.Count; i++)
            {
                int nodeIndex = traversal.fullPath[i];
                if (nodeIndex >= colors.Length) continue;
                
                // Check if this node should be overwritten by a newer path (legacy mode only)
                if (nodeToLatestPath.ContainsKey(nodeIndex))
                {
                    if (nodeToLatestPath[nodeIndex].pathId > traversal.pathId)
                    {
                        continue; // Skip this node, newer path owns it
                    }
                }
                
                // Apply persistent path color with fade
                float intensity = 0.4f * fadeAlpha; // Dimmer for persistent paths
                Color pathColor = Color.Lerp(traversal.color, inactiveColor, 0.3f); // Slightly dimmed
                colors[nodeIndex] = Color.Lerp(colors[nodeIndex], pathColor, intensity);
            }
        }
        
        void ApplyPathEffect(PathTraversal traversal, Color[] colors, bool isPersistent = false)
        {
            if (traversal.fullPath.Count == 0) return;
            
            // Show exploration effect if enabled
            if (showExploration)
            {
                foreach (int explored in traversal.exploredNodes)
                {
                    if (explored < colors.Length)
                    {
                        colors[explored] = Color.Lerp(colors[explored], exploredColor, 0.3f);
                    }
                }
            }
            
            // Light up the path with traveling effect
            for (int i = 0; i < traversal.fullPath.Count; i++)
            {
                int nodeIndex = traversal.fullPath[i];
                if (nodeIndex >= colors.Length) continue;
                
                float nodeProgress = (float)i / (traversal.fullPath.Count - 1);
                float intensity = 0f;
                
                if (legacyTravelingWave)
                {
                    // Legacy traveling wave mode
                    if (traversal.isComplete)
                    {
                        // Static path visualization
                        intensity = pathIntensity.Evaluate(nodeProgress) * 0.5f;
                    }
                    else
                    {
                        // Traveling wave effect
                        float distanceFromWave = Mathf.Abs(nodeProgress - traversal.totalProgress);
                        if (distanceFromWave < 0.2f) // Wave width
                        {
                            intensity = pathIntensity.Evaluate(1f - (distanceFromWave / 0.2f));
                        }
                        
                        // Highlight current position
                        if (i == traversal.currentNodeIndex)
                        {
                            intensity = Mathf.Max(intensity, 0.8f);
                        }
                        else if (i == traversal.currentNodeIndex + 1 && traversal.progress > 0f)
                        {
                            intensity = Mathf.Max(intensity, 0.5f * traversal.progress);
                        }
                    }
                }
                else
                {
                    // Legacy fill mode: Show entire path progressively filling
                    if (traversal.isComplete)
                    {
                        // Fully filled path
                        intensity = pathIntensity.Evaluate(nodeProgress) * 0.8f;
                    }
                    else
                    {
                        // Progressive fill based on total progress
                        if (nodeProgress <= traversal.totalProgress)
                        {
                            // This part of the path is filled
                            float fillIntensity = pathIntensity.Evaluate(nodeProgress);
                            
                            // Add extra brightness near the fill front
                            float distanceFromFront = traversal.totalProgress - nodeProgress;
                            if (distanceFromFront < 0.1f)
                            {
                                fillIntensity = Mathf.Max(fillIntensity, 0.9f);
                            }
                            
                            intensity = fillIntensity;
                        }
                        else
                        {
                            intensity = 0f; // Not filled yet
                        }
                    }
                }
                
                if (intensity > 0f)
                {
                    Color pathEffect = Color.Lerp(inactiveColor, traversal.color, intensity);
                    colors[nodeIndex] = Color.Lerp(colors[nodeIndex], pathEffect, 0.8f);
                }
            }
        }
        
        // Reset analysis when parameters change
        void OnValidate()
        {
            analysisComplete = false;
            activeTraversals.Clear();
            completedPaths.Clear();
            nodeToLatestPath.Clear();
            shortestPathsCache.Clear();
            paintedNetwork.Clear();
            currentHue = 0f;
            
            // Initialize default gradient if none exists
            if (pathColorGradient == null)
            {
                pathColorGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.red, 0f);
                colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);
                colorKeys[2] = new GradientColorKey(Color.blue, 1f);
                
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                
                pathColorGradient.SetKeys(colorKeys, alphaKeys);
            }
        }
    }
}
