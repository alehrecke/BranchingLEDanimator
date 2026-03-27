using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    [CreateAssetMenu(fileName = "ProgressivePathAnimation", menuName = "LED Animation/Progressive Path Animation")]
    public class ProgressivePathAnimation : LEDAnimationType
    {
        [Header("Path Settings")]
        [SerializeField] private float pathSpeed = 2.0f;
        [SerializeField] private float pathInterval = 1.5f;
        [SerializeField] private int maxConcurrentPaths = 4;
        [SerializeField] private bool useEndpointsOnly = true;
        
        [Header("Visual Settings")]
        [SerializeField] private Color basePathColor = Color.cyan;
        [SerializeField] private Color crossingColor = Color.white;
        [SerializeField] private bool useColorProgression = true;
        [SerializeField] private float colorHueShift = 0.15f;
        [SerializeField] private float fadeTime = 3.0f;
        
        [Header("Debug")]
        [SerializeField] private bool showEndpoints = true;
        [SerializeField] private Color endpointColor = Color.red;
        
        // Animation state
        private struct PathInfo
        {
            public List<int> nodes;
            public float startTime;
            public Color pathColor;
            public int pathId;
            public bool isComplete;
            public float progress; // 0-1 along the path
        }
        
        private List<int> endpoints;
        private List<PathInfo> activePaths;
        private Dictionary<int, Color> nodeColors; // Current color of each node
        private Dictionary<int, int> nodePathIds; // Which path currently owns each node
        private Dictionary<int, List<int>> nodeConnections;
        private float animationTime;
        private int nextPathId = 0;
        private float lastPathStartTime = 0;
        private float currentHue = 0;
        
        private void InitializeAnimation(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Always initialize collections to prevent null reference exceptions
            if (endpoints == null) endpoints = new List<int>();
            if (activePaths == null) activePaths = new List<PathInfo>();
            if (nodeColors == null) nodeColors = new Dictionary<int, Color>();
            if (nodePathIds == null) nodePathIds = new Dictionary<int, int>();
            if (nodeConnections == null) nodeConnections = new Dictionary<int, List<int>>();
            
            // Only do full initialization once
            if (nodeConnections.Count == 0)
            {
                BuildConnectionGraph(nodePositions, edgeConnections);
                FindEndpoints(nodePositions.Count);
                
                Debug.Log($"🎯 Progressive Path Animation initialized with {endpoints.Count} endpoints");
            }
        }
        
        private void BuildConnectionGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Initialize connections dictionary
            for (int i = 0; i < nodePositions.Count; i++)
            {
                nodeConnections[i] = new List<int>();
            }
            
            // Build bidirectional connections
            foreach (var connection in edgeConnections)
            {
                if (connection.x < nodePositions.Count && connection.y < nodePositions.Count)
                {
                    nodeConnections[connection.x].Add(connection.y);
                    nodeConnections[connection.y].Add(connection.x);
                }
            }
        }
        
        private void FindEndpoints(int nodeCount)
        {
            endpoints.Clear();
            
            for (int i = 0; i < nodeCount; i++)
            {
                int valence = nodeConnections[i].Count;
                
                if (useEndpointsOnly)
                {
                    // Only use actual endpoints (valence 1)
                    if (valence == 1)
                    {
                        endpoints.Add(i);
                    }
                }
                else
                {
                    // Include endpoints and some junction nodes
                    if (valence == 1 || (valence >= 3 && Random.value < 0.4f))
                    {
                        endpoints.Add(i);
                    }
                }
            }
            
            // Ensure we have at least 4 nodes to work with
            if (endpoints.Count < 4)
            {
                var additionalNodes = Enumerable.Range(0, nodeCount)
                    .Where(i => !endpoints.Contains(i))
                    .OrderBy(_ => Random.value)
                    .Take(4 - endpoints.Count);
                endpoints.AddRange(additionalNodes);
            }
        }
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            // Initialize animation if needed
            InitializeAnimation(nodePositions, edgeConnections);
            
            // Calculate animation time with speed and looping
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
            }
            
            // Calculate delta time for updates
            float deltaTime = animTime - animationTime;
            if (deltaTime < 0) deltaTime = 0.016f; // Reset case
            animationTime = animTime;
            
            // Start new paths periodically (with null check)
            if (activePaths != null && endpoints != null && endpoints.Count > 1 && 
                animationTime - lastPathStartTime > pathInterval && activePaths.Count < maxConcurrentPaths)
            {
                StartNewPath(nodePositions);
                lastPathStartTime = animationTime;
            }
            
            // Update existing paths (with null check)
            if (activePaths != null)
            {
                UpdatePaths(deltaTime, nodePositions);
            }
            
            // Generate final colors
            return GenerateColors(nodePositions.Count);
        }
        
        private void StartNewPath(List<Vector3> nodePositions)
        {
            if (endpoints.Count < 2) return;
            
            // Select random start and end points
            int startNode = endpoints[Random.Range(0, endpoints.Count)];
            int endNode;
            do {
                endNode = endpoints[Random.Range(0, endpoints.Count)];
            } while (endNode == startNode);
            
            // Find shortest path
            var path = FindShortestPath(startNode, endNode, nodePositions);
            if (path.Count < 2) return;
            
            // Generate path color
            Color pathColor;
            if (useColorProgression)
            {
                pathColor = Color.HSVToRGB(currentHue, 0.8f, 1.0f);
                currentHue += colorHueShift;
                if (currentHue > 1.0f) currentHue -= 1.0f;
            }
            else
            {
                pathColor = basePathColor;
            }
            
            // Create new path
            PathInfo newPath = new PathInfo
            {
                nodes = path,
                startTime = animationTime,
                pathColor = pathColor,
                pathId = nextPathId++,
                isComplete = false,
                progress = 0
            };
            
            activePaths.Add(newPath);
            Debug.Log($"🛤️ Started path {newPath.pathId}: Node {startNode} → Node {endNode} ({path.Count} nodes)");
        }
        
        private void UpdatePaths(float deltaTime, List<Vector3> nodePositions)
        {
            for (int i = activePaths.Count - 1; i >= 0; i--)
            {
                var path = activePaths[i];
                
                if (path.isComplete)
                {
                    // Remove completed paths after fade time
                    if (animationTime - path.startTime > fadeTime)
                    {
                        activePaths.RemoveAt(i);
                        Debug.Log($"🏁 Removed faded path {path.pathId}");
                    }
                    continue;
                }
                
                // Update path progress
                float pathDuration = path.nodes.Count / pathSpeed;
                float elapsedTime = animationTime - path.startTime;
                path.progress = Mathf.Clamp01(elapsedTime / pathDuration);
                
                // Check if path is complete
                if (path.progress >= 1.0f)
                {
                    path.isComplete = true;
                    path.progress = 1.0f;
                }
                
                // Apply path colors with overwriting logic
                ApplyPathColors(path);
                
                activePaths[i] = path;
            }
        }
        
        private void ApplyPathColors(PathInfo path)
        {
            // Safety checks
            if (path.nodes == null || nodeColors == null || nodePathIds == null) return;
            
            int nodesToLight = Mathf.FloorToInt(path.progress * path.nodes.Count);
            nodesToLight = Mathf.Clamp(nodesToLight, 0, path.nodes.Count);
            
            for (int i = 0; i < nodesToLight; i++)
            {
                int nodeIndex = path.nodes[i];
                
                // Check if this node is already owned by another path
                bool isOverwriting = nodePathIds.ContainsKey(nodeIndex) && nodePathIds[nodeIndex] != path.pathId;
                
                // Determine color
                Color nodeColor = isOverwriting ? 
                    Color.Lerp(path.pathColor, crossingColor, 0.6f) : 
                    path.pathColor;
                
                // Update node state
                nodeColors[nodeIndex] = nodeColor;
                nodePathIds[nodeIndex] = path.pathId;
            }
        }
        
        private Color[] GenerateColors(int nodeCount)
        {
            Color[] colors = new Color[nodeCount];
            
            // Initialize all nodes to inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Apply path colors (with null check)
            if (nodeColors != null)
            {
                foreach (var kvp in nodeColors)
                {
                    int nodeIndex = kvp.Key;
                    Color nodeColor = kvp.Value;
                    
                    if (nodeIndex < colors.Length)
                    {
                        colors[nodeIndex] = nodeColor;
                    }
                }
            }
            
            // Highlight endpoints if enabled (with null check)
            if (showEndpoints && endpoints != null)
            {
                float pulse = (Mathf.Sin(animationTime * 3.0f) + 1.0f) * 0.5f;
                foreach (int endpoint in endpoints)
                {
                    if (endpoint < colors.Length)
                    {
                        colors[endpoint] = Color.Lerp(colors[endpoint], endpointColor, pulse * 0.3f);
                    }
                }
            }
            
            return colors;
        }
        
        private List<int> FindShortestPath(int start, int end, List<Vector3> nodePositions)
        {
            // Dijkstra's algorithm
            var distances = new Dictionary<int, float>();
            var previous = new Dictionary<int, int>();
            var unvisited = new HashSet<int>();
            
            // Initialize
            foreach (int node in nodeConnections.Keys)
            {
                distances[node] = float.MaxValue;
                unvisited.Add(node);
            }
            distances[start] = 0;
            
            while (unvisited.Count > 0)
            {
                // Find closest unvisited node
                int current = unvisited.OrderBy(n => distances[n]).First();
                unvisited.Remove(current);
                
                if (current == end) break;
                if (distances[current] == float.MaxValue) break;
                
                // Check neighbors
                foreach (int neighbor in nodeConnections[current])
                {
                    if (!unvisited.Contains(neighbor)) continue;
                    
                    float distance = Vector3.Distance(nodePositions[current], nodePositions[neighbor]);
                    float altDistance = distances[current] + distance;
                    
                    if (altDistance < distances[neighbor])
                    {
                        distances[neighbor] = altDistance;
                        previous[neighbor] = current;
                    }
                }
            }
            
            // Reconstruct path
            var path = new List<int>();
            int currentNode = end;
            
            while (previous.ContainsKey(currentNode))
            {
                path.Add(currentNode);
                currentNode = previous[currentNode];
            }
            
            if (currentNode == start)
            {
                path.Add(start);
                path.Reverse();
                return path;
            }
            
            return new List<int>(); // No path found
        }
        
        // Reset animation state (called internally when loop resets)
        private void ResetAnimationState()
        {
            activePaths.Clear();
            nodeColors.Clear();
            nodePathIds.Clear();
            animationTime = 0;
            nextPathId = 0;
            lastPathStartTime = 0;
            currentHue = 0;
        }
    }
}
