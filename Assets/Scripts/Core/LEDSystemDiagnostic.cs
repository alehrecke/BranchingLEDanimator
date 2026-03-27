using UnityEngine;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Visualization;

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Diagnostic component to help troubleshoot LED system setup
    /// Add this temporarily to see what's happening with your system
    /// </summary>
    public class LEDSystemDiagnostic : MonoBehaviour
    {
        [Header("Diagnostic Results")]
        [SerializeField] private bool graphManagerFound = false;
        [SerializeField] private bool sceneVisualizerFound = false;
        [SerializeField] private bool animationSystemFound = false;
        [SerializeField] private bool dataFileExists = false;
        [SerializeField] private bool geometryLoaded = false;
        [SerializeField] private int nodeCount = 0;
        [SerializeField] private int edgeCount = 0;
        [SerializeField] private string dataFilePath = "";
        
        private LEDGraphManager graphManager;
        private LEDSceneVisualizer sceneVisualizer;
        private LEDAnimationSystem animationSystem;
        
        void Start()
        {
            RunDiagnostics();
        }
        
        #if UNITY_EDITOR
        void OnValidate()
        {
            // Run diagnostics when Inspector changes
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
                
            RunDiagnostics();
        }
        
        void OnEnable()
        {
            // Run diagnostics when enabled
            RunDiagnostics();
        }
        #endif
        
        [ContextMenu("Run Diagnostics")]
        public void RunDiagnostics()
        {
            Debug.Log("🔍 Running LED System Diagnostics...");
            
            // Check for required components
            graphManager = GetComponent<LEDGraphManager>();
            sceneVisualizer = GetComponent<LEDSceneVisualizer>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            
            graphManagerFound = graphManager != null;
            sceneVisualizerFound = sceneVisualizer != null;
            animationSystemFound = animationSystem != null;
            
            Debug.Log($"✓ Components Found:");
            Debug.Log($"  - LEDGraphManager: {(graphManagerFound ? "✅" : "❌")}");
            Debug.Log($"  - LEDSceneVisualizer: {(sceneVisualizerFound ? "✅" : "❌")}");
            Debug.Log($"  - LEDAnimationSystem: {(animationSystemFound ? "✅" : "❌")}");
            
            // Check data file
            if (graphManager != null)
            {
                dataFilePath = graphManager.grasshopperDataPath;
                dataFileExists = System.IO.File.Exists(dataFilePath);
                geometryLoaded = graphManager.DataLoaded;
                nodeCount = graphManager.NodeCount;
                edgeCount = graphManager.EdgeCount;
                
                Debug.Log($"✓ Data File Check:");
                Debug.Log($"  - Path: {dataFilePath}");
                Debug.Log($"  - File Exists: {(dataFileExists ? "✅" : "❌")}");
                Debug.Log($"  - Geometry Loaded: {(geometryLoaded ? "✅" : "❌")}");
                Debug.Log($"  - Node Count: {nodeCount}");
                Debug.Log($"  - Edge Count: {edgeCount}");
                
                if (dataFileExists && !geometryLoaded)
                {
                    Debug.Log("🔧 Data file exists but geometry not loaded. Attempting import...");
                    bool success = graphManager.ImportFromGrasshopper();
                    Debug.Log($"Import result: {(success ? "✅ Success" : "❌ Failed")}");
                    
                    // Update counts after import
                    geometryLoaded = graphManager.DataLoaded;
                    nodeCount = graphManager.NodeCount;
                    edgeCount = graphManager.EdgeCount;
                }
            }
            
            // Check Scene view settings
            if (sceneVisualizer != null)
            {
                Debug.Log($"✓ Scene Visualizer Settings:");
                Debug.Log($"  - Show In Scene View: {sceneVisualizer.showInSceneView}");
                Debug.Log($"  - Show Nodes: {sceneVisualizer.showNodes}");
                Debug.Log($"  - Show Connections: {sceneVisualizer.showConnections}");
                Debug.Log($"  - Node Size: {sceneVisualizer.nodeSize}");
            }
            
            // Check animation system
            if (animationSystem != null)
            {
                Debug.Log($"✓ Animation System:");
                Debug.Log($"  - Animation Count: {animationSystem.AnimationCount}");
                Debug.Log($"  - Is Playing: {animationSystem.isPlaying}");
                Debug.Log($"  - Auto Play: {animationSystem.autoPlayOnStart}");
                Debug.Log($"  - Current Animation: {animationSystem.CurrentAnimationName}");
            }
            
            // Final recommendations
            Debug.Log("🎯 Recommendations:");
            if (!graphManagerFound)
                Debug.LogWarning("❌ Add LEDGraphManager component");
            if (!sceneVisualizerFound)
                Debug.LogWarning("❌ Add LEDSceneVisualizer component");
            if (!animationSystemFound)
                Debug.LogWarning("❌ Add LEDAnimationSystem component");
            if (!dataFileExists)
                Debug.LogWarning("❌ Check grasshopper data file path");
            if (dataFileExists && !geometryLoaded)
                Debug.LogWarning("❌ Geometry import failed - check file format");
            if (geometryLoaded && nodeCount == 0)
                Debug.LogWarning("❌ No nodes found - check data file content");
                
            if (graphManagerFound && sceneVisualizerFound && dataFileExists && geometryLoaded && nodeCount > 0)
            {
                Debug.Log("🎉 System looks good! You should see geometry in Scene view.");
                Debug.Log("💡 Try selecting this GameObject and looking at the Scene view.");
                Debug.Log("💡 Make sure Scene view Gizmos are enabled (button in top-right of Scene view).");
            }
        }
        
        [ContextMenu("Force Scene View Refresh")]
        public void ForceSceneViewRefresh()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            Debug.Log("🔄 Forced Scene view refresh");
            #endif
        }
        
        [ContextMenu("Focus Scene View on Geometry")]
        public void FocusSceneView()
        {
            if (sceneVisualizer != null)
            {
                sceneVisualizer.FocusSceneView();
            }
            else
            {
                Debug.LogWarning("No LEDSceneVisualizer found!");
            }
        }
        
        [ContextMenu("Debug Animation System")]
        public void DebugAnimationSystem()
        {
            if (animationSystem != null)
            {
                animationSystem.DebugAnimationState();
            }
            else
            {
                Debug.LogWarning("No LEDAnimationSystem found!");
            }
        }
        
        [ContextMenu("Start Animation")]
        public void StartAnimation()
        {
            if (animationSystem != null)
            {
                Debug.Log($"🎬 Attempting to start animation...");
                Debug.Log($"  - Before: Is Playing = {animationSystem.isPlaying}");
                Debug.Log($"  - Animation Count = {animationSystem.AnimationCount}");
                Debug.Log($"  - Current Animation = {animationSystem.CurrentAnimationName}");
                
                animationSystem.Play();
                
                Debug.Log($"  - After: Is Playing = {animationSystem.isPlaying}");
                Debug.Log("✅ Animation start command sent");
            }
            else
            {
                Debug.LogWarning("No LEDAnimationSystem found!");
            }
        }
        
        [ContextMenu("Analyze Graph for LED Mapping")]
        public void AnalyzeGraphForLEDMapping()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogError("No graph data available for analysis!");
                return;
            }
            
            Debug.Log("🔍 ANALYZING GRAPH STRUCTURE FOR LED CIRCUIT MAPPING");
            Debug.Log("============================================================");
            
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            var sourceNodes = graphManager.SourceNodes;
            
            // Basic stats
            Debug.Log($"📊 BASIC STATISTICS:");
            Debug.Log($"  - Total Nodes (LEDs needed): {nodePositions.Count}");
            Debug.Log($"  - Total Connections: {edgeConnections.Count}");
            Debug.Log($"  - Source Nodes (endpoints): {sourceNodes.Count}");
            
            // Analyze node connectivity (branching points)
            Debug.Log($"\n🌿 BRANCHING ANALYSIS:");
            var connectionCounts = new System.Collections.Generic.Dictionary<int, int>();
            
            foreach (var edge in edgeConnections)
            {
                if (!connectionCounts.ContainsKey(edge.x)) connectionCounts[edge.x] = 0;
                if (!connectionCounts.ContainsKey(edge.y)) connectionCounts[edge.y] = 0;
                connectionCounts[edge.x]++;
                connectionCounts[edge.y]++;
            }
            
            int branchPoints = 0;
            int endpoints = 0;
            int linearNodes = 0;
            
            foreach (var kvp in connectionCounts)
            {
                if (kvp.Value == 1) endpoints++;
                else if (kvp.Value == 2) linearNodes++;
                else if (kvp.Value > 2) branchPoints++;
            }
            
            Debug.Log($"  - Branch Points (3+ connections): {branchPoints}");
            Debug.Log($"  - Linear Nodes (2 connections): {linearNodes}");
            Debug.Log($"  - Endpoints (1 connection): {endpoints}");
            
            // Suggest LED strip configuration
            Debug.Log($"\n💡 LED STRIP RECOMMENDATIONS:");
            
            if (branchPoints > 0)
            {
                Debug.Log($"  - COMPLEX BRANCHING DETECTED!");
                Debug.Log($"  - Recommended: Multiple ESP32 controllers");
                Debug.Log($"  - Estimated strips needed: {branchPoints + 1} to {sourceNodes.Count}");
                Debug.Log($"  - Power injection points needed: ~{nodePositions.Count / 100} locations");
            }
            else
            {
                Debug.Log($"  - Simple linear/chain structure detected");
                Debug.Log($"  - Single ESP32 could handle this");
                Debug.Log($"  - Estimated strips: 1-2 long strips");
            }
            
            Debug.Log($"\n🔌 TECHNICAL REQUIREMENTS:");
            Debug.Log($"  - Total LED power (est.): {nodePositions.Count * 0.06f:F1}W @ 5V");
            Debug.Log($"  - Data rate needed: ~{nodePositions.Count * 30 * 3}bps @ 30fps");
            Debug.Log($"  - Recommended LED type: WS2812B or SK6812");
            
            Debug.Log("============================================================");
        }
        
        [ContextMenu("Force Update Animation")]
        public void ForceUpdateAnimation()
        {
            if (animationSystem != null && graphManager != null)
            {
                Debug.Log("🔄 Force updating animation colors...");
                
                // Get current colors
                var colors = graphManager.CurrentNodeColors;
                if (colors != null)
                {
                    Debug.Log($"Current colors array length: {colors.Length}");
                    Debug.Log($"First few colors: {colors[0]}, {colors[1]}, {colors[2]}");
                }
                else
                {
                    Debug.Log("❌ No colors array found!");
                }
                
                // Try to manually trigger color update
                if (animationSystem.CurrentAnimation != null)
                {
                    var newColors = animationSystem.CurrentAnimation.CalculateNodeColors(
                        graphManager.NodePositions,
                        graphManager.EdgeConnections,
                        graphManager.SourceNodes,
                        0f, // time = 0
                        0   // frame = 0
                    );
                    
                    graphManager.UpdateColors(newColors);
                    Debug.Log($"✅ Manually updated {newColors.Length} colors");
                }
            }
        }
    }
}
