using UnityEngine;
using System.Collections.Generic;

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Represents the actual LED hardware layout as a graph.
    /// This is derived from the circuit mapper and provides LED-centric
    /// positions and topology for animations that want pixel-perfect control.
    /// 
    /// Unlike the original node graph (from polyline import), this graph has:
    /// - One vertex per physical LED
    /// - Edges connecting adjacent LEDs on the same strip
    /// - Topology matching actual hardware wiring
    /// </summary>
    [System.Serializable]
    public class MappedGraph
    {
        /// <summary>
        /// World-space position of each LED.
        /// Index = global LED index (0 to TotalLEDCount-1)
        /// </summary>
        public List<Vector3> LEDPositions = new List<Vector3>();
        
        /// <summary>
        /// Edges connecting adjacent LEDs.
        /// Each Vector2Int contains (ledIndexA, ledIndexB) for connected LEDs.
        /// </summary>
        public List<Vector2Int> LEDEdges = new List<Vector2Int>();
        
        /// <summary>
        /// LED indices that are endpoints (first or last LED of endpoint strips).
        /// These are the "touchable" points in interactive animations.
        /// </summary>
        public List<int> LEDEndpoints = new List<int>();
        
        /// <summary>
        /// Maps each LED index to its strip index.
        /// </summary>
        public List<int> LEDToStripIndex = new List<int>();
        
        /// <summary>
        /// Maps each LED index to its position within its strip (0 to stripLEDCount-1).
        /// </summary>
        public List<int> LEDToStripPosition = new List<int>();
        
        /// <summary>
        /// Maps each LED to its normalized progress along its strip (0.0 to 1.0).
        /// Useful for animations that work with normalized positions.
        /// </summary>
        public List<float> LEDProgress = new List<float>();
        
        /// <summary>
        /// Number of LEDs per strip, indexed by strip index.
        /// </summary>
        public List<int> StripLEDCounts = new List<int>();
        
        /// <summary>
        /// First LED index for each strip (global index where strip starts).
        /// </summary>
        public List<int> StripStartIndices = new List<int>();
        
        /// <summary>
        /// Total number of LEDs in the mapped graph.
        /// </summary>
        public int TotalLEDCount => LEDPositions.Count;
        
        /// <summary>
        /// Number of strips in the mapped graph.
        /// </summary>
        public int StripCount => StripLEDCounts.Count;
        
        /// <summary>
        /// Check if the mapped graph has been generated and is valid.
        /// </summary>
        public bool IsValid => LEDPositions.Count > 0 && LEDToStripIndex.Count == LEDPositions.Count;
        
        /// <summary>
        /// Get all LED indices belonging to a specific strip.
        /// </summary>
        public List<int> GetLEDsForStrip(int stripIndex)
        {
            var leds = new List<int>();
            if (stripIndex < 0 || stripIndex >= StripCount) return leds;
            
            int start = StripStartIndices[stripIndex];
            int count = StripLEDCounts[stripIndex];
            
            for (int i = 0; i < count; i++)
            {
                leds.Add(start + i);
            }
            return leds;
        }
        
        /// <summary>
        /// Get the LED indices that form a path between two endpoint LEDs.
        /// Uses BFS to find the shortest path through the LED graph.
        /// </summary>
        public List<int> FindPath(int startLED, int endLED)
        {
            if (startLED < 0 || startLED >= TotalLEDCount || 
                endLED < 0 || endLED >= TotalLEDCount)
                return new List<int>();
            
            // Build adjacency from edges
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < TotalLEDCount; i++)
                adjacency[i] = new List<int>();
            
            foreach (var edge in LEDEdges)
            {
                adjacency[edge.x].Add(edge.y);
                adjacency[edge.y].Add(edge.x);
            }
            
            // BFS
            var visited = new HashSet<int>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { startLED });
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                int current = path[path.Count - 1];
                
                if (current == endLED) return path;
                if (visited.Contains(current)) continue;
                visited.Add(current);
                
                foreach (int neighbor in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        var newPath = new List<int>(path) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }
            
            return new List<int>();
        }
        
        /// <summary>
        /// Clear all data in the mapped graph.
        /// </summary>
        public void Clear()
        {
            LEDPositions.Clear();
            LEDEdges.Clear();
            LEDEndpoints.Clear();
            LEDToStripIndex.Clear();
            LEDToStripPosition.Clear();
            LEDProgress.Clear();
            StripLEDCounts.Clear();
            StripStartIndices.Clear();
        }
        
        /// <summary>
        /// Get debug summary of the mapped graph.
        /// </summary>
        public string GetSummary()
        {
            return $"MappedGraph: {TotalLEDCount} LEDs across {StripCount} strips, " +
                   $"{LEDEndpoints.Count} endpoints, {LEDEdges.Count} edges";
        }
    }
}
