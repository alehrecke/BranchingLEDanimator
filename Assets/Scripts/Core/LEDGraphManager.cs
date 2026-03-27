using UnityEngine;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Single source of truth for LED graph geometry and state data
    /// Handles Grasshopper import and provides clean data access for all systems
    /// </summary>
    public class LEDGraphManager : MonoBehaviour
    {
        [Header("Import Settings")]
        public string grasshopperDataPath = "Assets/Data/grasshopper_export.txt";
        public bool autoImportOnStart = true;
        public bool autoReloadOnFileChange = true; // Monitor file changes and auto-reload
        
        [Header("Graph Data (Read-Only)")]
        [SerializeField] private List<Vector3> nodePositions = new List<Vector3>();
        [SerializeField] private List<Vector2Int> edgeConnections = new List<Vector2Int>();
        [SerializeField] private List<int> sourceNodes = new List<int>();
        
        [Header("Polyline Data (For Circuit Mapping)")]
        [SerializeField] private List<PolylineInfo> polylines = new List<PolylineInfo>();
        
        [Header("Current State")]
        [SerializeField] private Color[] currentNodeColors;
        [SerializeField] private bool dataLoaded = false;
        
        [Header("Debug")]
        public bool showDebugInfo = true;
        
        #if UNITY_EDITOR
        // File monitoring
        private System.DateTime lastFileWriteTime;
        private string lastFilePath = "";
        private float fileCheckInterval = 1f; // Check every second
        private float lastFileCheckTime = 0f;
        #endif
        
        // Public read-only access to data
        public List<Vector3> NodePositions => nodePositions;
        public List<Vector2Int> EdgeConnections => edgeConnections;
        public List<int> SourceNodes => sourceNodes;
        public Color[] CurrentNodeColors => currentNodeColors;
        public bool DataLoaded => dataLoaded;
        public int NodeCount => nodePositions.Count;
        public int EdgeCount => edgeConnections.Count;
        
        // Polyline access for circuit mapping
        public List<PolylineInfo> Polylines => polylines;
        public int PolylineCount => polylines.Count;
        
        void Start()
        {
            // Auto-import geometry on start if enabled
            if (autoImportOnStart && !dataLoaded)
            {
                ImportFromGrasshopper();
            }
            
            #if UNITY_EDITOR
            // Initialize file monitoring
            InitializeFileMonitoring();
            #endif
        }
        
        #if UNITY_EDITOR
        void Update()
        {
            // Check for file changes periodically (only in editor)
            if (autoReloadOnFileChange && dataLoaded && Time.time > lastFileCheckTime + fileCheckInterval)
            {
                CheckForFileChanges();
                lastFileCheckTime = Time.time;
            }
        }
        #endif
        
        #if UNITY_EDITOR
        void OnValidate()
        {
            // Called when Inspector values change or component is added
            if (autoImportOnStart && !dataLoaded && !string.IsNullOrEmpty(grasshopperDataPath))
            {
                // Import in Edit mode when component is first added or values change
                ImportFromGrasshopper();
            }
        }
        
        void OnEnable()
        {
            // Import geometry when component is enabled (works in Edit mode)
            if (autoImportOnStart && !dataLoaded)
            {
                ImportFromGrasshopper();
            }
        }
        #endif
        
        /// <summary>
        /// Import geometry data from Grasshopper export file
        /// </summary>
        [ContextMenu("🔄 Reload Geometry from Grasshopper")]
        public bool ImportFromGrasshopper()
        {
            return ImportFromGrasshopper(grasshopperDataPath);
        }
        
        /// <summary>
        /// Force reload geometry data (useful when Grasshopper file has been updated)
        /// </summary>
        [ContextMenu("🔄 Force Reload Geometry")]
        public bool ForceReloadGeometry()
        {
            if (showDebugInfo)
            {
                Debug.Log($"🔄 LEDGraphManager: Force reloading geometry from {grasshopperDataPath}");
                Debug.Log($"🔄 Debug info enabled: {showDebugInfo}");
            }
            dataLoaded = false; // Reset loaded flag
            return ImportFromGrasshopper(grasshopperDataPath);
        }
        
        /// <summary>
        /// Import geometry data from specified file path
        /// </summary>
        public bool ImportFromGrasshopper(string filePath)
        {
            if (showDebugInfo)
            {
                Debug.Log($"📁 LEDGraphManager: Starting import from {filePath}");
            }
            
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Grasshopper data file not found: {filePath}");
                return false;
            }
            
            try
            {
                if (showDebugInfo)
                {
                    Debug.Log($"🧹 LEDGraphManager: Clearing existing data...");
                }
                ClearData();
                
                string[] lines = File.ReadAllLines(filePath);
                
                int polylineCount = 0;
                int totalEdges = 0;
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    // Skip comment lines
                    if (line.Trim().StartsWith("#")) continue;
                    
                    // Parse polyline (multiple points per line)
                    string[] parts = line.Split(',');
                    if (parts.Length < 6) continue; // Need at least 2 points (6 coordinates)
                    
                    // Parse all points in this polyline
                    List<Vector3> polylinePoints = new List<Vector3>();
                    
                    for (int i = 0; i < parts.Length; i += 3)
                    {
                        if (i + 2 < parts.Length) // Ensure we have x, y, z
                        {
                            if (float.TryParse(parts[i], out float x) &&
                                float.TryParse(parts[i + 1], out float y) &&
                                float.TryParse(parts[i + 2], out float z))
                            {
                                Vector3 point = new Vector3(x, y, z);
                                polylinePoints.Add(point);
                            }
                        }
                    }
                    
                    // Create edges between consecutive points in the polyline
                    if (polylinePoints.Count >= 2)
                    {
                        polylineCount++;
                        
                        // Create PolylineInfo for circuit mapping
                        var polylineInfo = new PolylineInfo(polylineCount - 1, $"Polyline {polylineCount}");
                        polylineInfo.points = new List<Vector3>(polylinePoints);
                        
                        for (int i = 0; i < polylinePoints.Count - 1; i++)
                        {
                            Vector3 startPos = polylinePoints[i];
                            Vector3 endPos = polylinePoints[i + 1];
                            
                            int startIndex = AddOrGetNodeIndex(startPos);
                            int endIndex = AddOrGetNodeIndex(endPos);
                            
                            // Track node indices for this polyline
                            if (!polylineInfo.nodeIndices.Contains(startIndex))
                                polylineInfo.nodeIndices.Add(startIndex);
                            if (!polylineInfo.nodeIndices.Contains(endIndex))
                                polylineInfo.nodeIndices.Add(endIndex);
                            
                            // Add edge connection
                            Vector2Int edge = new Vector2Int(startIndex, endIndex);
                            if (!edgeConnections.Contains(edge))
                            {
                                edgeConnections.Add(edge);
                                totalEdges++;
                            }
                        }
                        
                        // Calculate polyline properties
                        polylineInfo.CalculateLength();
                        polylineInfo.CalculateSuggestedLEDCount(1f); // 1 LED per unit length
                        
                        // Add to polylines list
                        polylines.Add(polylineInfo);
                    }
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"✓ Processed {polylineCount} polylines, created {totalEdges} edges");
                    Debug.Log($"✓ Total nodes: {nodePositions.Count}");
                    Debug.Log($"✓ Polylines tracked: {polylines.Count} (for circuit mapping)");
                    
                    // Debug polyline info
                    for (int i = 0; i < polylines.Count; i++)
                    {
                        var p = polylines[i];
                        Debug.Log($"  - {p.polylineName}: {p.points.Count} points, {p.nodeIndices.Count} nodes, length={p.totalLength:F2}, suggested LEDs={p.suggestedLEDCount}");
                    }
                }
                
                // Find source nodes (naked nodes with only one connection)
                FindSourceNodes();
                
                // Initialize color array
                currentNodeColors = new Color[nodePositions.Count];
                for (int i = 0; i < currentNodeColors.Length; i++)
                {
                    currentNodeColors[i] = Color.black;
                }
                
                dataLoaded = true;
                
                if (showDebugInfo)
                {
                    Debug.Log($"✓ FINAL IMPORT RESULTS:");
                    Debug.Log($"  - Total Nodes (LEDs): {nodePositions.Count}");
                    Debug.Log($"  - Total Edges (Connections): {edgeConnections.Count}");
                    Debug.Log($"  - Source Nodes (Naked): {sourceNodes.Count}");
                    Debug.Log($"  - Geometry Bounds: {GetGeometryBounds()}");
                    
                    // Show first few nodes for verification
                    Debug.Log($"  - First 5 LED positions:");
                    for (int i = 0; i < Mathf.Min(5, nodePositions.Count); i++)
                    {
                        Debug.Log($"    LED {i}: {nodePositions[i]}");
                    }
                }
                
                // Notify other systems
                LEDVisualizationEvents.TriggerGeometryUpdated(nodePositions, edgeConnections, sourceNodes);
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to import Grasshopper data: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Update the current LED colors (called by animation system)
        /// </summary>
        public void UpdateColors(Color[] newColors)
        {
            if (newColors == null || newColors.Length != nodePositions.Count)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("Color array size mismatch!");
                }
                return;
            }
            
            currentNodeColors = new Color[newColors.Length];
            System.Array.Copy(newColors, currentNodeColors, newColors.Length);
            
            // Notify visualization systems
            LEDVisualizationEvents.TriggerColorsUpdated(currentNodeColors);
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Initialize file monitoring system
        /// </summary>
        void InitializeFileMonitoring()
        {
            if (!string.IsNullOrEmpty(grasshopperDataPath) && File.Exists(grasshopperDataPath))
            {
                lastFilePath = grasshopperDataPath;
                lastFileWriteTime = File.GetLastWriteTime(grasshopperDataPath);
                
                if (showDebugInfo)
                {
                    Debug.Log($"📁 File monitoring initialized for: {grasshopperDataPath}");
                }
            }
        }
        
        /// <summary>
        /// Check if the Grasshopper file has been modified
        /// </summary>
        void CheckForFileChanges()
        {
            if (string.IsNullOrEmpty(grasshopperDataPath) || !File.Exists(grasshopperDataPath))
                return;
            
            // Check if path changed
            if (lastFilePath != grasshopperDataPath)
            {
                InitializeFileMonitoring();
                return;
            }
            
            // Check if file was modified
            System.DateTime currentWriteTime = File.GetLastWriteTime(grasshopperDataPath);
            if (currentWriteTime > lastFileWriteTime)
            {
                lastFileWriteTime = currentWriteTime;
                
                if (showDebugInfo)
                {
                    Debug.Log($"🔄 Grasshopper file updated - auto-reloading geometry...");
                }
                
                // Reload the geometry
                ForceReloadGeometry();
            }
        }
        #endif
        
        /// <summary>
        /// Clear all geometry data
        /// </summary>
        public void ClearData()
        {
            nodePositions.Clear();
            edgeConnections.Clear();
            sourceNodes.Clear();
            polylines.Clear();
            currentNodeColors = null;
            dataLoaded = false;
            
            if (showDebugInfo)
            {
                if (showDebugInfo)
                {
                    Debug.Log("✓ Cleared all geometry data");
                }
            }
        }
        
        /// <summary>
        /// Get bounds of the geometry for camera positioning
        /// </summary>
        public Bounds GetGeometryBounds()
        {
            if (nodePositions.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one);
            
            Vector3 min = nodePositions[0];
            Vector3 max = nodePositions[0];
            
            foreach (Vector3 pos in nodePositions)
            {
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
            
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            
            return new Bounds(center, size);
        }
        
        /// <summary>
        /// Get data ready for ESP32 mapping
        /// </summary>
        public LEDMappingData GetMappingData()
        {
            return new LEDMappingData
            {
                nodePositions = new List<Vector3>(nodePositions),
                nodeColors = currentNodeColors != null ? (Color[])currentNodeColors.Clone() : new Color[nodePositions.Count],
                sourceNodes = new List<int>(sourceNodes),
                totalNodes = nodePositions.Count
            };
        }
        
        /// <summary>
        /// Add node or get existing node index
        /// </summary>
        private int AddOrGetNodeIndex(Vector3 position)
        {
            const float tolerance = 0.01f;
            
            // Check if node already exists at this position
            for (int i = 0; i < nodePositions.Count; i++)
            {
                if (Vector3.Distance(nodePositions[i], position) < tolerance)
                {
                    return i;
                }
            }
            
            // Add new node
            nodePositions.Add(position);
            return nodePositions.Count - 1;
        }
        
        /// <summary>
        /// Find source nodes (naked nodes with only one connection)
        /// </summary>
        private void FindSourceNodes()
        {
            sourceNodes.Clear();
            
            // Count connections for each node
            int[] connectionCounts = new int[nodePositions.Count];
            
            foreach (Vector2Int edge in edgeConnections)
            {
                if (edge.x >= 0 && edge.x < connectionCounts.Length)
                    connectionCounts[edge.x]++;
                if (edge.y >= 0 && edge.y < connectionCounts.Length)
                    connectionCounts[edge.y]++;
            }
            
            // Find nodes with only one connection
            for (int i = 0; i < connectionCounts.Length; i++)
            {
                if (connectionCounts[i] == 1)
                {
                    sourceNodes.Add(i);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Found {sourceNodes.Count} source nodes (naked nodes)");
            }
        }
    }
    
    /// <summary>
    /// Data structure for ESP32 LED mapping
    /// </summary>
    [System.Serializable]
    public class LEDMappingData
    {
        public List<Vector3> nodePositions;
        public Color[] nodeColors;
        public List<int> sourceNodes;
        public int totalNodes;
    }
    
    /// <summary>
    /// Information about a single polyline from Grasshopper
    /// Used for circuit mapping while preserving graph structure for animations
    /// </summary>
    [System.Serializable]
    public class PolylineInfo
    {
        [Header("Polyline Identity")]
        public int polylineIndex;           // Index in original Grasshopper export
        public string polylineName;         // User-friendly name
        
        [Header("Geometry")]
        public List<Vector3> points = new List<Vector3>();     // Original polyline points
        public List<int> nodeIndices = new List<int>();        // Corresponding graph node indices
        
        [Header("Physical Properties")]
        public int suggestedLEDCount;       // Auto-calculated based on length
        public float totalLength;          // Physical length of polyline
        
        public PolylineInfo(int index, string name)
        {
            polylineIndex = index;
            polylineName = name;
            points = new List<Vector3>();
            nodeIndices = new List<int>();
        }
        
        /// <summary>
        /// Calculate total length of the polyline
        /// </summary>
        public void CalculateLength()
        {
            totalLength = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                totalLength += Vector3.Distance(points[i], points[i + 1]);
            }
        }
        
        /// <summary>
        /// Suggest LED count based on length (one LED per unit length, minimum 1)
        /// </summary>
        public void CalculateSuggestedLEDCount(float ledsPerUnit = 1f)
        {
            suggestedLEDCount = Mathf.Max(1, Mathf.RoundToInt(totalLength * ledsPerUnit));
        }
    }
}
