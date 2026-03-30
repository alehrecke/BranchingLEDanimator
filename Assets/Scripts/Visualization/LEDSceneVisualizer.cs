using UnityEngine;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Visualization
{
    /// <summary>
    /// Simple Scene view visualizer using Gizmos
    /// Shows LED nodes and connections in Scene view (works in Edit mode)
    /// </summary>
    public class LEDSceneVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        public bool showInSceneView = true;
        public bool showNodes = true;
        public bool showConnections = true;
        public bool showNodeLabels = false;
        public bool autoFocusOnLoad = true;
        
        [Header("Node Display")]
        public float nodeSize = 2f;
        public bool useWireframeSpheres = true;
        public Color defaultNodeColor = Color.white;
        public Color sourceNodeColor = Color.yellow;
        
        [Header("Connection Display")]
        public Color connectionColor = Color.gray;
        public float connectionAlpha = 0.5f;
        
        [Header("Animation")]
        public bool showAnimatedColors = true;
        public Color inactiveNodeColor = Color.black;
        
        [Header("Debug")]
        public bool enableDebugLogging = false;
        
        // Internal references
        private LEDGraphManager graphManager;
        
        void Start()
        {
            InitializeReferences();
        }
        
        #if UNITY_EDITOR
        void OnEnable()
        {
            // Initialize in Edit mode too
            InitializeReferences();
            
            // Subscribe to editor update for real-time Scene view updates
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        
        void OnDisable()
        {
            // Unsubscribe from editor update
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            
            // Unsubscribe from events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
        }
        
        void OnValidate()
        {
            // Reinitialize when Inspector changes
            InitializeReferences();
        }
        #endif
        
        void InitializeReferences()
        {
            // Get reference to graph manager on same GameObject
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            if (graphManager == null)
            {
                Debug.LogError("LEDSceneVisualizer requires LEDGraphManager on the same GameObject!");
                return;
            }
            
            // Subscribe to events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated; // Prevent double subscription
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated; // Prevent double subscription  
            LEDVisualizationEvents.OnColorsUpdated += OnColorsUpdated;
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryUpdated;
            LEDVisualizationEvents.OnColorsUpdated -= OnColorsUpdated;
            
            #if UNITY_EDITOR
            // Unsubscribe from editor update
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            #endif
        }
        
        /// <summary>
        /// Handle geometry update event
        /// </summary>
        private void OnGeometryUpdated(LEDGraphManager source,
                                     System.Collections.Generic.List<Vector3> nodePositions, 
                                     System.Collections.Generic.List<Vector2Int> edgeConnections, 
                                     System.Collections.Generic.List<int> sourceNodes)
        {
            if (this == null) return;
            if (source != graphManager) return;
            if (autoFocusOnLoad && showInSceneView)
            {
                StartCoroutine(DelayedFocus());
            }
        }
        
        /// <summary>
        /// Handle color updates from animation system
        /// </summary>
        private void OnColorsUpdated(LEDGraphManager source, Color[] colors)
        {
            if (this == null) return;
            if (source != graphManager) return;
            if (colors != null && colors.Length > 0)
            {
                Color firstColor = colors[0];
                if (enableDebugLogging)
            {
                Debug.Log($"🎨 LEDSceneVisualizer received {colors.Length} colors. First color: r{firstColor.r:F3} g{firstColor.g:F3} b{firstColor.b:F3}");
            }
            }
            
            // LEDSceneVisualizer uses the colors directly from graphManager.CurrentNodeColors
            // in OnDrawGizmos, so we don't need to store them here.
            // This method exists to receive the event and trigger Scene View refresh.
            
            #if UNITY_EDITOR
            // Force Scene View to repaint with new colors
            UnityEditor.SceneView.RepaintAll();
            #endif
        }
        
        /// <summary>
        /// Delayed focus to ensure geometry is ready
        /// </summary>
        private System.Collections.IEnumerator DelayedFocus()
        {
            yield return new WaitForEndOfFrame();
            FocusSceneView();
        }
        
        void OnDrawGizmos()
        {
            // Always try to draw something for debugging
            if (graphManager == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
                return;
            }
            
            if (!graphManager.DataLoaded)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Vector3.zero, 2f);
                return;
            }
            
            if (!showInSceneView)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                return;
            }
            
            // Draw relative to the sculpture's transform so geometry moves with the GameObject
            Gizmos.matrix = transform.localToWorldMatrix;
            DrawConnections();
            DrawNodes();
            Gizmos.matrix = Matrix4x4.identity;
        }
        
        void OnDrawGizmosSelected()
        {
            if (graphManager == null || !graphManager.DataLoaded)
                return;
            
            Gizmos.matrix = transform.localToWorldMatrix;
            DrawConnections();
            DrawNodes();
            
            if (showNodeLabels)
            {
                DrawNodeLabels();
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
        
        /// <summary>
        /// Draw LED nodes as spheres
        /// </summary>
        private void DrawNodes()
        {
            if (!showNodes) 
            {
                // Draw debug info if nodes are disabled
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(Vector3.zero, 0.2f);
                return;
            }
            
            var nodePositions = graphManager.NodePositions;
            var sourceNodes = graphManager.SourceNodes;
            var nodeColors = graphManager.CurrentNodeColors;
            
            if (nodePositions == null || nodePositions.Count == 0)
            {
                // Draw debug info if no positions
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(Vector3.zero, 3f);
                return;
            }
            
            for (int i = 0; i < nodePositions.Count; i++)
            {
                Color nodeColor = GetNodeColor(i, nodeColors, sourceNodes);
                
                Gizmos.color = nodeColor;
                
                if (useWireframeSpheres)
                {
                    Gizmos.DrawWireSphere(nodePositions[i], nodeSize);
                }
                else
                {
                    Gizmos.DrawSphere(nodePositions[i], nodeSize);
                }
            }
        }
        
        /// <summary>
        /// Draw connections between nodes
        /// </summary>
        private void DrawConnections()
        {
            if (!showConnections) return;
            
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            
            Color lineColor = connectionColor;
            lineColor.a = connectionAlpha;
            Gizmos.color = lineColor;
            
            foreach (var edge in edgeConnections)
            {
                if (edge.x >= 0 && edge.x < nodePositions.Count &&
                    edge.y >= 0 && edge.y < nodePositions.Count)
                {
                    Gizmos.DrawLine(nodePositions[edge.x], nodePositions[edge.y]);
                }
            }
        }
        
        /// <summary>
        /// Draw node index labels
        /// </summary>
        private void DrawNodeLabels()
        {
            var nodePositions = graphManager.NodePositions;
            
            #if UNITY_EDITOR
            for (int i = 0; i < nodePositions.Count; i++)
            {
                Vector3 worldPos = transform.TransformPoint(nodePositions[i]);
                Vector3 screenPos = UnityEditor.HandleUtility.WorldToGUIPoint(worldPos);
                
                UnityEditor.Handles.BeginGUI();
                GUI.Label(new Rect(screenPos.x - 10, screenPos.y - 10, 20, 20), i.ToString());
                UnityEditor.Handles.EndGUI();
            }
            #endif
        }
        
        /// <summary>
        /// Get appropriate color for a node
        /// </summary>
        private Color GetNodeColor(int nodeIndex, Color[] nodeColors, System.Collections.Generic.List<int> sourceNodes)
        {
            // Use animated color if available and animation is enabled
            if (showAnimatedColors && nodeColors != null && nodeIndex < nodeColors.Length)
            {
                Color animatedColor = nodeColors[nodeIndex];
                
                // If color is black/inactive, show default color with low alpha
                if (animatedColor == Color.black || (animatedColor.r + animatedColor.g + animatedColor.b) < 0.01f)
                {
                    Color inactive = inactiveNodeColor;
                    inactive.a = 0.3f;
                    return inactive;
                }
                
                // Boost dim colors for better Scene view visibility
                float maxComponent = Mathf.Max(animatedColor.r, animatedColor.g, animatedColor.b);
                if (maxComponent > 0.01f && maxComponent < 0.5f)
                {
                    // Amplify dim colors (like 0.170 red) to at least 70% brightness
                    float boostFactor = 0.7f / maxComponent;
                    animatedColor = new Color(
                        animatedColor.r * boostFactor,
                        animatedColor.g * boostFactor,
                        animatedColor.b * boostFactor,
                        animatedColor.a
                    );
                }
                
                return animatedColor;
            }
            
            // Show source nodes in special color
            if (sourceNodes.Contains(nodeIndex))
            {
                return sourceNodeColor;
            }
            
            // Default node color
            return defaultNodeColor;
        }
        
        /// <summary>
        /// Focus Scene view on the geometry
        /// </summary>
        public void FocusSceneView()
        {
            if (graphManager == null || !graphManager.DataLoaded)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning("No geometry data to focus on!");
                }
                return;
            }
            
            #if UNITY_EDITOR
            Bounds bounds = graphManager.GetGeometryBounds();
            
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.Frame(bounds, false);
                sceneView.Repaint();
                
                if (enableDebugLogging)
            {
                Debug.Log($"✓ Focused Scene view on geometry bounds: {bounds}");
            }
            }
            #endif
        }
        
        /// <summary>
        /// Toggle Scene view visibility
        /// </summary>
        public void ToggleSceneView()
        {
            showInSceneView = !showInSceneView;
            
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }
        
        /// <summary>
        /// Refresh Scene view display
        /// </summary>
        public void RefreshSceneView()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }
        
        // Editor-only methods for real-time updates
        #if UNITY_EDITOR
        private void OnEditorUpdate()
        {
            // Force Scene view repaint for animation updates in Edit mode
            if (showInSceneView && showAnimatedColors && graphManager != null && graphManager.DataLoaded)
            {
                UnityEditor.SceneView.RepaintAll();
            }
        }
        #endif
    }
}
