using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Graph topology-based animation that analyzes node valence and creates pulsing effects
    /// </summary>
    [CreateAssetMenu(fileName = "GraphTopologyAnimation", menuName = "LED Animations/Graph Topology Animation")]
    public class GraphTopologyAnimation : LEDAnimationType
    {
        [Header("Node Classification")]
        [SerializeField] private int highValenceThreshold = 2; // Nodes with > this many connections = junctions
        [SerializeField] private int lowValenceThreshold = 1;  // Nodes with <= this many connections = endpoints
        
        [Header("Pulse Behavior")]
        [SerializeField] private bool simultaneousPulses = false; // All sources pulse together vs sequential
        [SerializeField] private bool breathingMode = true; // Fill/unfill effect vs expanding wave
        [SerializeField] private float breathingCycles = 2f; // Breathing cycles per duration
        
        [Header("Pulse Properties")]
        [SerializeField] private float pulseRadius = 5f; // How far pulses spread
        [SerializeField] private AnimationCurve pulseFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0); // Intensity falloff curve
        
        [Header("Color Configuration")]
        [SerializeField] private bool usePrimarySecondaryColors = true; // Use primaryColor/secondaryColor instead of specific node colors
        
        // Graph analysis cache
        private List<int> highValenceNodes = new List<int>();
        private List<int> lowValenceNodes = new List<int>();
        private Dictionary<int, int> nodeValences = new Dictionary<int, int>();
        private Dictionary<int, List<int>> nodeConnections = new Dictionary<int, List<int>>();
        private Dictionary<int, float> distancesFromSources = new Dictionary<int, float>();
        private bool analysisComplete = false;
        
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
                AnalyzeGraphTopology(nodePositions, edgeConnections);
            }
            
            // Apply base colors for special nodes
            ApplyBaseColors(colors);
            
            // Calculate pulse effects
            float animTime = time * speed;
            if (loop && duration > 0)
            {
                animTime = animTime % duration;
            }
            
            ApplyPulseEffects(colors, animTime);
            
            return colors;
        }
        
        void AnalyzeGraphTopology(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Clear previous analysis
            highValenceNodes.Clear();
            lowValenceNodes.Clear();
            nodeValences.Clear();
            nodeConnections.Clear();
            
            // Initialize node connections
            for (int i = 0; i < nodePositions.Count; i++)
            {
                nodeConnections[i] = new List<int>();
                nodeValences[i] = 0;
            }
            
            // Analyze edge connections to determine valence
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
                }
            }
            
            // Categorize nodes by valence
            foreach (var kvp in nodeValences)
            {
                int nodeIndex = kvp.Key;
                int valence = kvp.Value;
                
                if (valence > highValenceThreshold)
                {
                    highValenceNodes.Add(nodeIndex);
                }
                else if (valence <= lowValenceThreshold)
                {
                    lowValenceNodes.Add(nodeIndex);
                }
            }
            
            // Calculate distances from all source nodes for pulse propagation
            CalculateDistancesFromSources(nodePositions);
            
            analysisComplete = true;
        }
        
        void CalculateDistancesFromSources(List<Vector3> nodePositions)
        {
            distancesFromSources.Clear();
            
            // Calculate minimum distance from any source node using BFS
            var allSources = new List<int>();
            allSources.AddRange(highValenceNodes);
            allSources.AddRange(lowValenceNodes);
            
            // Initialize distances
            for (int i = 0; i < nodePositions.Count; i++)
            {
                distancesFromSources[i] = float.MaxValue;
            }
            
            // Set source distances to 0
            foreach (int source in allSources)
            {
                distancesFromSources[source] = 0f;
            }
            
            // BFS to calculate graph distances
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            
            foreach (int source in allSources)
            {
                queue.Enqueue(source);
                visited.Add(source);
            }
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                float currentDistance = distancesFromSources[current];
                
                if (nodeConnections.ContainsKey(current))
                {
                    foreach (int neighbor in nodeConnections[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            distancesFromSources[neighbor] = currentDistance + 1f;
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }
            }
        }
        
        void ApplyBaseColors(Color[] colors)
        {
            // Get colors to use for node types
            Color highColor = usePrimarySecondaryColors ? primaryColor : secondaryColor;
            Color lowColor = usePrimarySecondaryColors ? secondaryColor : primaryColor;
            
            // Dim base colors for source nodes
            foreach (int node in highValenceNodes)
            {
                if (node < colors.Length)
                {
                    colors[node] = Color.Lerp(inactiveColor, highColor, 0.3f);
                }
            }
            
            foreach (int node in lowValenceNodes)
            {
                if (node < colors.Length)
                {
                    colors[node] = Color.Lerp(inactiveColor, lowColor, 0.3f);
                }
            }
        }
        
        void ApplyPulseEffects(Color[] colors, float animTime)
        {
            // Get colors to use for pulses
            Color highColor = usePrimarySecondaryColors ? primaryColor : secondaryColor;
            Color lowColor = usePrimarySecondaryColors ? secondaryColor : primaryColor;
            
            if (simultaneousPulses)
            {
                // All sources pulse together - use same timing as sequential mode for consistent speed
                var allSources = new List<int>();
                allSources.AddRange(highValenceNodes);
                allSources.AddRange(lowValenceNodes);
                
                float effectiveSpeed = allSources.Count > 0 ? allSources.Count : 1f;
                float pulseTime = ((animTime * effectiveSpeed) % duration) / duration;
                
                ApplyPulsesFromNodes(colors, highValenceNodes, highColor, pulseTime);
                ApplyPulsesFromNodes(colors, lowValenceNodes, lowColor, pulseTime);
            }
            else
            {
                // Sequential pulses from different sources
                var allSources = new List<int>();
                allSources.AddRange(highValenceNodes);
                allSources.AddRange(lowValenceNodes);
                
                if (allSources.Count > 0)
                {
                    // Calculate which source should be pulsing based on time
                    float pulseInterval = duration / allSources.Count;
                    int activeSourceIndex = Mathf.FloorToInt(animTime / pulseInterval) % allSources.Count;
                    float pulseTime = (animTime % pulseInterval) / pulseInterval;
                    
                    int sourceNode = allSources[activeSourceIndex];
                    bool isHighValence = highValenceNodes.Contains(sourceNode);
                    Color pulseColor = isHighValence ? highColor : lowColor;
                    
                    ApplyPulseFromNode(colors, sourceNode, pulseColor, pulseTime);
                }
            }
        }
        
        void ApplyPulsesFromNodes(Color[] colors, List<int> sourceNodes, Color pulseColor, float pulseTime)
        {
            foreach (int sourceNode in sourceNodes)
            {
                ApplyPulseFromNode(colors, sourceNode, pulseColor, pulseTime);
            }
        }
        
        void ApplyPulseFromNode(Color[] colors, int sourceNode, Color pulseColor, float pulseTime)
        {
            if (sourceNode >= colors.Length) return;
            
            float currentRadius;
            
            if (breathingMode)
            {
                // Fill/unfill breathing effect: oscillates between 0 and pulseRadius using sine wave
                float breathingPhase = pulseTime * breathingCycles * 2f * Mathf.PI;
                float breathingAmplitude = (Mathf.Sin(breathingPhase) + 1f) * 0.5f; // 0 to 1 range
                currentRadius = breathingAmplitude * pulseRadius;
            }
            else
            {
                // Expanding wave pulse effect
                currentRadius = pulseTime * pulseRadius;
            }
            
            for (int i = 0; i < colors.Length; i++)
            {
                // Calculate distance from pulse source using graph connectivity
                float graphDistance = distancesFromSources.ContainsKey(i) ? distancesFromSources[i] : float.MaxValue;
                
                if (graphDistance != float.MaxValue)
                {
                    float intensity = 0f;
                    
                    if (breathingMode)
                    {
                        // Fill/unfill breathing mode: light up all nodes within current radius
                        if (graphDistance <= currentRadius)
                        {
                            float normalizedDistance = currentRadius > 0 ? graphDistance / currentRadius : 0f;
                            intensity = pulseFalloff.Evaluate(1f - normalizedDistance);
                            
                            // Add extra intensity at the source node
                            if (i == sourceNode)
                            {
                                float sourceIntensity = (Mathf.Sin(pulseTime * breathingCycles * 2f * Mathf.PI) + 1f) * 0.5f;
                                intensity = Mathf.Max(intensity, sourceIntensity * 0.8f);
                            }
                        }
                    }
                    else
                    {
                        // Expanding wave pulse mode
                        if (graphDistance <= currentRadius)
                        {
                            float distanceFromPulseEdge = Mathf.Abs(currentRadius - graphDistance);
                            float normalizedDistance = Mathf.Clamp01(distanceFromPulseEdge / 2f);
                            intensity = pulseFalloff.Evaluate(1f - normalizedDistance);
                            
                            if (i == sourceNode)
                            {
                                intensity = Mathf.Max(intensity, 0.8f);
                            }
                        }
                    }
                    
                    if (intensity > 0f)
                    {
                        // Use the pulse color directly instead of blending with propagation color
                        // This fixes the issue where propagation color wasn't working
                        colors[i] = Color.Lerp(colors[i], pulseColor, intensity);
                    }
                }
            }
        }
        
        // Reset analysis when parameters change
        void OnValidate()
        {
            analysisComplete = false;
        }
    }
}
