using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Animation;
using BranchingLEDAnimator.Hardware;

namespace BranchingLEDAnimator.Core
{
    /// <summary>
    /// Simplified animation system that uses existing LEDAnimationType assets
    /// Calculates colors and updates the LEDGraphManager
    /// </summary>
    public class LEDAnimationSystem : MonoBehaviour
    {
        [Header("Animation")]
        public List<LEDAnimationType> availableAnimations = new List<LEDAnimationType>();
        public int currentAnimationIndex = 0;
        public bool isPlaying = false;
        public bool autoPlayOnStart = true;
        
        [Header("Timing")]
        public float globalSpeed = 1f;
        public bool useFixedTimeStep = false;
        public float fixedTimeStep = 0.1f;
        
        [Header("Scene View Animation")]
        [Tooltip("When enabled, animation always plays in Scene view during Edit mode")]
        public bool alwaysPlayInSceneView = true;
        
        [Header("Brightness")]
        [Range(0f, 1f)]
        public float globalBrightness = 1f;
        
        [Header("Debug")]
        public bool showDebugInfo = false;
        
        // Brightness preset methods (accessible via right-click context menu)
        [ContextMenu("Set Brightness - Dim (25%)")]
        public void SetBrightnessDim() => globalBrightness = 0.25f;
        
        [ContextMenu("Set Brightness - Medium (50%)")]
        public void SetBrightnessMedium() => globalBrightness = 0.5f;
        
        [ContextMenu("Set Brightness - Bright (75%)")]
        public void SetBrightnessBright() => globalBrightness = 0.75f;
        
        [ContextMenu("Set Brightness - Max (100%)")]
        public void SetBrightnessMax() => globalBrightness = 1.0f;
        
        [ContextMenu("Test Animation System")]
        public void TestAnimationSystem()
        {
            Debug.Log("🔍 Testing Animation System...");
            Debug.Log($"   - Is Playing: {isPlaying}");
            Debug.Log($"   - Current Animation: {CurrentAnimationName}");
            Debug.Log($"   - Animation Count: {AnimationCount}");
            Debug.Log($"   - Graph Manager: {(graphManager != null ? "Found" : "Missing")}");
            
            if (graphManager != null)
            {
                Debug.Log($"   - Data Loaded: {graphManager.DataLoaded}");
                Debug.Log($"   - Node Count: {graphManager.NodeCount}");
            }
            
            if (CurrentAnimation != null && graphManager != null && graphManager.DataLoaded)
            {
                Debug.Log("✅ System ready - manually triggering color update...");
                UpdateAnimationColors();
            }
            else
            {
                Debug.LogError("❌ System not ready for animation");
            }
        }
        
        // Internal state
        private LEDGraphManager graphManager;
        private LEDCircuitMapper circuitMapper;
        private float animationStartTime;
        private int currentFrame = 0;
        private float lastUpdateTime;
        private int lastAnimationIndex = -1;
        
        // Properties
        public LEDAnimationType CurrentAnimation => 
            availableAnimations.Count > 0 && currentAnimationIndex >= 0 && currentAnimationIndex < availableAnimations.Count 
                ? availableAnimations[currentAnimationIndex] 
                : null;
        
        // Use asset name (what shows in Inspector) rather than animationName field        
        public string CurrentAnimationName => CurrentAnimation?.name ?? "None";
        public int AnimationCount => availableAnimations.Count;
        
        void Start()
        {
            InitializeSystem();
        }
        
        #if UNITY_EDITOR
        void OnEnable()
        {
            // Initialize in Edit mode too
            InitializeSystem();
            
            // Register for Editor updates (works in Edit mode)
            UnityEditor.EditorApplication.update += EditorUpdate;
        }
        
        void OnDisable()
        {
            // Unregister from Editor updates
            UnityEditor.EditorApplication.update -= EditorUpdate;
        }
        
        void OnValidate()
        {
            // Reinitialize when Inspector changes
            InitializeSystem();
            
            // Validate when Inspector changes
            ValidateAnimationIndex();
            
            // Auto-start/restart continuous animation if everything is ready
            bool shouldAutoStart = (autoPlayOnStart || alwaysPlayInSceneView) && 
                                   graphManager != null && 
                                   graphManager.DataLoaded && 
                                   availableAnimations.Count > 0;
            
            if (shouldAutoStart)
            {
                // Small delay to ensure Inspector changes are fully processed
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return; // Safety check
                    
                    bool stillShouldStart = (autoPlayOnStart || alwaysPlayInSceneView) && 
                                           graphManager != null && 
                                           graphManager.DataLoaded && 
                                           availableAnimations.Count > 0;
                    
                    if (stillShouldStart)
                    {
                        // Re-register editor update to ensure it's running
                        UnityEditor.EditorApplication.update -= EditorUpdate;
                        UnityEditor.EditorApplication.update += EditorUpdate;
                        
                        // Set playing state
                        isPlaying = true;
                        continuousAnimationRunning = true;
                    }
                };
                #endif
            }
        }
        
        /// <summary>
        /// Editor update loop (works in Edit mode)
        /// </summary>
        void EditorUpdate()
        {
            debugUpdateCount++; // Count how many times this is called
            
            if (!Application.isPlaying)
            {
                // Auto-restart if alwaysPlayInSceneView is enabled but animation stopped
                if (alwaysPlayInSceneView && !isPlaying && graphManager != null && graphManager.DataLoaded && availableAnimations.Count > 0)
                {
                    isPlaying = true;
                    continuousAnimationRunning = true;
                }
                
                // Run animation updates in Edit mode
                AnimationUpdate();
                
                // Force Scene view repaint for animation updates
                if (isPlaying && graphManager != null && graphManager.DataLoaded)
                {
                    UnityEditor.SceneView.RepaintAll();
                }
            }
        }
        
        private static int debugUpdateCount = 0;
        [ContextMenu("Check Update Loop Status")]
        public void CheckUpdateLoopStatus()
        {
            int startCount = debugUpdateCount;
            
            Debug.Log($"🔍 Checking if EditorUpdate is running...");
            Debug.Log($"Current update count: {debugUpdateCount}");
            
            // Wait a moment and check again
            System.Threading.Tasks.Task.Delay(1000).ContinueWith(t =>
            {
                UnityEngine.Debug.Log($"Update count after 1 second: {debugUpdateCount}");
                UnityEngine.Debug.Log($"Updates per second: {debugUpdateCount - startCount}");
                
                if (debugUpdateCount == startCount)
                {
                    UnityEngine.Debug.LogWarning("❌ EditorUpdate is NOT running!");
                }
                else
                {
                    UnityEngine.Debug.Log("✅ EditorUpdate is running!");
                }
            });
        }
        
        private bool continuousAnimationRunning = false; // Changed from static to instance
        
        [ContextMenu("Start Continuous Animation")]
        public void StartContinuousAnimation()
        {
            StartContinuousAnimationInternal(forceRestart: true);
            Debug.Log("🎬 Manual continuous animation started via context menu");
        }
        
        /// <summary>
        /// Internal method to start continuous animation (used by auto-start and manual)
        /// </summary>
        private void StartContinuousAnimationInternal(bool forceRestart = false)
        {
            // Allow force restart even if already running
            if (continuousAnimationRunning && !forceRestart) return;
            
            isPlaying = true;
            continuousAnimationRunning = true;
            
            // Only reset animation timing if not force restarting (to preserve animation state during edits)
            if (!forceRestart || !continuousAnimationRunning)
            {
                ResetAnimation();
            }
            
            #if UNITY_EDITOR
            // Use multiple update mechanisms for robustness
            UnityEditor.EditorApplication.update -= EditorUpdate;
            UnityEditor.EditorApplication.update += EditorUpdate;
            
            // Also use delayCall for backup updates
            ScheduleNextUpdate();
            #endif
            
            if (showDebugInfo)
            {
                Debug.Log("✅ Continuous animation started - watch Scene view!");
            }
        }
        
        [ContextMenu("Stop Continuous Animation")]
        public void StopContinuousAnimation()
        {
            if (showDebugInfo)
            {
                Debug.Log("⏹️ Stopping continuous animation...");
            }
            isPlaying = false;
            continuousAnimationRunning = false;
            
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
            #endif
        }
        
        [ContextMenu("Test Wave Looping")]
        public void TestWaveLooping()
        {
            if (CurrentAnimation != null)
            {
                Debug.Log($"🌊 Testing Wave animation looping...");
                Debug.Log($"Animation: {CurrentAnimation.animationName}");
                Debug.Log($"Duration: {CurrentAnimation.duration}s");
                Debug.Log($"Loop enabled: {CurrentAnimation.loop}");
                Debug.Log($"Speed: {CurrentAnimation.speed}");
                
                if (CurrentAnimation.animationName.Contains("Wave"))
                {
                    Debug.Log("✅ This is a Wave animation - should loop every 10 seconds");
                }
                else
                {
                    Debug.Log("ℹ️ This is not a Wave animation");
                }
            }
        }
        
        #if UNITY_EDITOR
        private void ScheduleNextUpdate()
        {
            if (continuousAnimationRunning && isPlaying)
            {
                // Schedule the next update
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (continuousAnimationRunning && isPlaying)
                    {
                        // Perform animation update
                        if (!Application.isPlaying && graphManager != null && graphManager.DataLoaded)
                        {
                            AnimationUpdate();
                            UnityEditor.SceneView.RepaintAll();
                        }
                        
                        // Schedule next update (approximately 30 FPS)
                        System.Threading.Tasks.Task.Delay(33).ContinueWith(t =>
                        {
                            UnityEditor.EditorApplication.delayCall += () => ScheduleNextUpdate();
                        });
                    }
                };
            }
        }
        #endif
        
        [ContextMenu("Debug Update Loop")]
        public void DebugUpdateLoop()
        {
            Debug.Log("🔄 Testing update loop...");
            
            for (int i = 0; i < 5; i++)
            {
                float currentTime = GetCurrentTime();
                float animationTime = (currentTime - animationStartTime) * globalSpeed;
                
                Debug.Log($"Frame {i}: time={animationTime:F2}");
                
                if (CurrentAnimation != null && graphManager != null && graphManager.DataLoaded)
                {
                    Color[] colors = CurrentAnimation.CalculateNodeColors(
                        graphManager.NodePositions,
                        graphManager.EdgeConnections, 
                        graphManager.SourceNodes,
                        animationTime,
                        i
                    );
                    
                    Debug.Log($"  Colors: {colors[0]}, {colors[1]}, {colors[2]}");
                    
                    if (i == 4) // Apply the last frame
                    {
                        graphManager.UpdateColors(colors);
                        #if UNITY_EDITOR
                        UnityEditor.SceneView.RepaintAll();
                        #endif
                    }
                }
                
                // Advance time by 0.1 seconds for next frame
                animationStartTime -= 0.1f;
            }
            
            Debug.Log("✅ Update loop test complete");
        }
        #endif
        
        [ContextMenu("Debug Animation State")]
        public void DebugAnimationState()
        {
            Debug.Log($"🔍 Animation System Debug:");
            Debug.Log($"  - Is Playing: {isPlaying}");
            Debug.Log($"  - Animation Count: {availableAnimations.Count}");
            Debug.Log($"  - Current Index: {currentAnimationIndex}");
            Debug.Log($"  - Current Animation: {CurrentAnimationName}");
            Debug.Log($"  - Graph Manager: {(graphManager != null ? "✅" : "❌")}");
            Debug.Log($"  - Data Loaded: {(graphManager != null ? graphManager.DataLoaded : false)}");
            Debug.Log($"  - Application Playing: {Application.isPlaying}");
            Debug.Log($"  - Auto Play On Start: {autoPlayOnStart}");
            
            // Check timing
            float currentTime = GetCurrentTime();
            Debug.Log($"  - Current Time: {currentTime:F2}");
            Debug.Log($"  - Animation Start Time: {animationStartTime:F2}");
            Debug.Log($"  - Animation Duration: {(currentTime - animationStartTime):F2}s");
        }
        
        [ContextMenu("Test Single Animation Update")]
        public void TestSingleAnimationUpdate()
        {
            Debug.Log("🧪 Testing single animation update...");
            
            if (CurrentAnimation == null)
            {
                Debug.LogError("No current animation!");
                return;
            }
            
            if (graphManager == null || !graphManager.DataLoaded)
            {
                Debug.LogError("No graph manager or data not loaded!");
                return;
            }
            
            float currentTime = GetCurrentTime();
            float animationTime = (currentTime - animationStartTime) * globalSpeed;
            
            Debug.Log($"Calculating colors with time={animationTime:F2}, frame={currentFrame}");
            Debug.Log($"Source nodes count: {graphManager.SourceNodes.Count}");
            Debug.Log($"First few source nodes: {(graphManager.SourceNodes.Count > 0 ? graphManager.SourceNodes[0].ToString() : "none")}");
            Debug.Log($"Animation settings: primaryColor={CurrentAnimation.primaryColor}, inactiveColor={CurrentAnimation.inactiveColor}");
            
            Color[] nodeColors = CurrentAnimation.CalculateNodeColors(
                graphManager.NodePositions,
                graphManager.EdgeConnections,
                graphManager.SourceNodes,
                animationTime,
                currentFrame
            );
            
            Debug.Log($"Got {nodeColors.Length} colors. First few: {nodeColors[0]}, {nodeColors[1]}, {nodeColors[2]}");
            
            // Update graph manager
            graphManager.UpdateColors(nodeColors);
            
            // Force Scene view refresh
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
            
            Debug.Log("✅ Single update complete - check Scene view!");
        }
        
        void InitializeSystem()
        {
            // Get reference to graph manager on same GameObject
            if (graphManager == null)
            {
                graphManager = GetComponent<LEDGraphManager>();
            }
            
            // Get reference to circuit mapper for LED-based animations
            if (circuitMapper == null)
            {
                circuitMapper = GetComponent<LEDCircuitMapper>();
                
                // Also try to find it in scene if not on same GameObject
                if (circuitMapper == null)
                {
                    circuitMapper = FindFirstObjectByType<LEDCircuitMapper>();
                }
            }
            
            if (graphManager == null)
            {
                Debug.LogError("LEDAnimationSystem requires LEDGraphManager on the same GameObject!");
                return;
            }
            
            // Load default animations if none assigned
            if (availableAnimations.Count == 0)
            {
                LoadDefaultAnimations();
            }
            
            // Validate animation index
            ValidateAnimationIndex();
            
            // Initialize timing
            ResetAnimation();
            lastAnimationIndex = currentAnimationIndex;
            
            // Subscribe to geometry updates to auto-start animation
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryLoaded; // Prevent double subscription
            LEDVisualizationEvents.OnGeometryUpdated += OnGeometryLoaded;
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ LEDAnimationSystem initialized with {availableAnimations.Count} animations");
                Debug.Log($"✓ Graph Manager found: {(graphManager != null ? "✅" : "❌")}");
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            LEDVisualizationEvents.OnGeometryUpdated -= OnGeometryLoaded;
        }
        
        /// <summary>
        /// Handle geometry loaded event
        /// </summary>
        private void OnGeometryLoaded(System.Collections.Generic.List<Vector3> nodePositions, 
                                    System.Collections.Generic.List<Vector2Int> edgeConnections, 
                                    System.Collections.Generic.List<int> sourceNodes)
        {
            if (autoPlayOnStart && !isPlaying && availableAnimations.Count > 0)
            {
                // Start continuous animation automatically
                StartContinuousAnimationInternal();
                
                if (showDebugInfo)
                {
                    Debug.Log("✓ Auto-started continuous animation after geometry load");
                }
            }
        }
        
        void Update()
        {
            if (Application.isPlaying)
            {
                // Run animation updates in Play mode
                AnimationUpdate();
            }
        }
        
        /// <summary>
        /// Unified animation update method (works in both Edit and Play mode)
        /// </summary>
        void AnimationUpdate()
        {
            // Check for animation change
            if (lastAnimationIndex != currentAnimationIndex)
            {
                OnAnimationChanged();
                lastAnimationIndex = currentAnimationIndex;
            }
            
            // Update animation if playing
            if (isPlaying && CurrentAnimation != null && graphManager != null && graphManager.DataLoaded)
            {
                UpdateAnimationColors();
            }
        }
        
        /// <summary>
        /// Update animation colors and send to graph manager
        /// </summary>
        private void UpdateAnimationColors()
        {
            bool shouldUpdate = false;
            
            float currentTime = GetCurrentTime();
            
            if (useFixedTimeStep)
            {
                // Fixed time step updates
                if (currentTime - lastUpdateTime >= fixedTimeStep)
                {
                    currentFrame++;
                    lastUpdateTime = currentTime;
                    shouldUpdate = true;
                }
            }
            else
            {
                // Continuous updates
                shouldUpdate = true;
            }
            
            if (shouldUpdate)
            {
                // Calculate current animation time
                float animationTime = (currentTime - animationStartTime) * globalSpeed;
                
                Color[] nodeColors = null;
                
                // Try LED-based animation first if supported
                bool supportsLED = CurrentAnimation.SupportsLEDMode;
                bool hasMapper = circuitMapper != null;
                
                if (supportsLED && hasMapper)
                {
                    // GetMappedGraph will auto-generate if needed
                    var mappedGraph = circuitMapper.GetMappedGraph();
                    bool hasValidGraph = mappedGraph != null && mappedGraph.IsValid;
                    
                    // Debug once per animation
                    if (currentFrame == 0)
                    {
                        Debug.Log($"[LEDAnimationSystem] '{CurrentAnimation.name}' LED mode: " +
                            $"hasValidGraph={hasValidGraph}, LEDs={mappedGraph?.TotalLEDCount ?? 0}, " +
                            $"Positions={mappedGraph?.LEDPositions?.Count ?? 0}");
                    }
                    
                    if (hasValidGraph)
                    {
                        Color[] ledColors = CurrentAnimation.CalculateEnhancedLEDColors(mappedGraph, animationTime, currentFrame);
                        
                        if (ledColors != null && ledColors.Length > 0)
                        {
                            // Check if LED colors are actually populated (not all black)
                            bool hasColor = false;
                            for (int c = 0; c < Mathf.Min(ledColors.Length, 50); c++)
                            {
                                if (ledColors[c].maxColorComponent > 0.01f)
                                {
                                    hasColor = true;
                                    break;
                                }
                            }
                            
                            if (hasColor)
                            {
                                // LED-based animation - colors go directly to hardware
                                // Still need node colors for visualization, so we map back
                                nodeColors = MapLEDColorsToNodes(ledColors, mappedGraph);
                            }
                            else if (currentFrame == 0)
                            {
                                Debug.LogWarning($"[LEDAnimationSystem] '{CurrentAnimation.name}' returned all black LED colors - falling back to node mode");
                            }
                        }
                        else if (currentFrame == 0)
                        {
                            Debug.LogWarning($"[LEDAnimationSystem] '{CurrentAnimation.name}' returned null/empty LED colors");
                        }
                    }
                }
                
                // Fall back to node-based animation
                if (nodeColors == null)
                {
                    nodeColors = CurrentAnimation.CalculateNodeColors(
                        graphManager.NodePositions,
                        graphManager.EdgeConnections,
                        graphManager.SourceNodes,
                        animationTime,
                        currentFrame
                    );
                }
                
                // Apply global brightness multiplier
                if (globalBrightness < 1f)
                {
                    for (int i = 0; i < nodeColors.Length; i++)
                    {
                        nodeColors[i] *= globalBrightness;
                    }
                }
                
                // Update graph manager with new colors
                graphManager.UpdateColors(nodeColors);
            }
        }
        
        /// <summary>
        /// Map LED colors back to node colors for visualization.
        /// Used when animation runs in LED mode but we still need node colors for Unity display.
        /// </summary>
        private Color[] MapLEDColorsToNodes(Color[] ledColors, MappedGraph mappedGraph)
        {
            int nodeCount = graphManager.NodePositions.Count;
            Color[] nodeColors = new Color[nodeCount];
            
            // Initialize with inactive color
            for (int i = 0; i < nodeCount; i++)
            {
                nodeColors[i] = CurrentAnimation.inactiveColor;
            }
            
            // For each LED, find closest node and apply color
            // This is approximate but good enough for visualization
            var nodePositions = graphManager.NodePositions;
            
            for (int led = 0; led < ledColors.Length && led < mappedGraph.LEDPositions.Count; led++)
            {
                Vector3 ledPos = mappedGraph.LEDPositions[led];
                
                // Find closest node
                int closestNode = 0;
                float closestDist = float.MaxValue;
                
                for (int n = 0; n < nodePositions.Count; n++)
                {
                    float dist = Vector3.SqrMagnitude(ledPos - nodePositions[n]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestNode = n;
                    }
                }
                
                // Use brightest color at this node (max blend)
                Color existing = nodeColors[closestNode];
                Color ledColor = ledColors[led];
                if (ledColor.maxColorComponent > existing.maxColorComponent)
                {
                    nodeColors[closestNode] = ledColor;
                }
            }
            
            return nodeColors;
        }
        
        /// <summary>
        /// Start/resume animation
        /// </summary>
        public void Play()
        {
            if (!isPlaying)
            {
                isPlaying = true;
                LEDVisualizationEvents.TriggerAnimationPlayStateChanged(true);
                
                if (showDebugInfo)
                {
                    Debug.Log($"▶ Started animation: {CurrentAnimationName}");
                }
            }
        }
        
        /// <summary>
        /// Pause animation
        /// </summary>
        public void Pause()
        {
            if (isPlaying)
            {
                isPlaying = false;
                LEDVisualizationEvents.TriggerAnimationPlayStateChanged(false);
                
                if (showDebugInfo)
                {
                    Debug.Log($"⏸ Paused animation: {CurrentAnimationName}");
                }
            }
        }
        
        /// <summary>
        /// Stop and reset animation
        /// </summary>
        public void Stop()
        {
            isPlaying = false;
            ResetAnimation();
            LEDVisualizationEvents.TriggerAnimationPlayStateChanged(false);
            
            if (showDebugInfo)
            {
                Debug.Log($"⏹ Stopped animation: {CurrentAnimationName}");
            }
        }
        
        /// <summary>
        /// Toggle play/pause
        /// </summary>
        public void TogglePlayPause()
        {
            if (isPlaying)
                Pause();
            else
                Play();
        }
        
        /// <summary>
        /// Reset animation timing
        /// </summary>
        public void ResetAnimation()
        {
            float currentTime = GetCurrentTime();
            animationStartTime = currentTime;
            lastUpdateTime = currentTime;
            currentFrame = 0;
        }
        
        /// <summary>
        /// Get current time (works in both Edit and Play mode)
        /// </summary>
        private float GetCurrentTime()
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
            #endif
            return Time.time;
        }
        
        /// <summary>
        /// Set animation by index
        /// </summary>
        public void SetAnimation(int index)
        {
            if (index >= 0 && index < availableAnimations.Count && index != currentAnimationIndex)
            {
                currentAnimationIndex = index;
                // Change will be handled in Update()
            }
        }
        
        /// <summary>
        /// Set animation by name
        /// </summary>
        public void SetAnimation(string animationName)
        {
            for (int i = 0; i < availableAnimations.Count; i++)
            {
                if (availableAnimations[i].animationName == animationName)
                {
                    SetAnimation(i);
                    return;
                }
            }
            
            if (showDebugInfo)
            {
                Debug.LogWarning($"Animation '{animationName}' not found!");
            }
        }
        
        /// <summary>
        /// Get list of available animation names (uses asset name, not animationName field)
        /// </summary>
        public string[] GetAnimationNames()
        {
            string[] names = new string[availableAnimations.Count];
            for (int i = 0; i < availableAnimations.Count; i++)
            {
                if (availableAnimations[i] != null)
                {
                    // Use the asset name (what shows in Inspector) rather than the animationName field
                    names[i] = availableAnimations[i].name;
                }
                else
                {
                    names[i] = "Null";
                }
            }
            return names;
        }
        
        /// <summary>
        /// Find and load all animation assets in project
        /// </summary>
        [ContextMenu("Find All Animation Assets")]
        public void FindAllAnimationAssets()
        {
            #if UNITY_EDITOR
            availableAnimations.Clear();
            
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LEDAnimationType");
            
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                LEDAnimationType animation = UnityEditor.AssetDatabase.LoadAssetAtPath<LEDAnimationType>(path);
                
                if (animation != null)
                {
                    availableAnimations.Add(animation);
                }
            }
            
            ValidateAnimationIndex();
            
            if (showDebugInfo)
            {
                Debug.Log($"✓ Found {availableAnimations.Count} animation assets");
            }
            #endif
        }
        
        /// <summary>
        /// Load default animations (fallback)
        /// </summary>
        private void LoadDefaultAnimations()
        {
            // Try to find existing animation assets first
            FindAllAnimationAssets();
            
            // If still none found, create runtime defaults
            if (availableAnimations.Count == 0)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning("No animation assets found. Create animation assets using: Assets → Create → LED Animation");
                }
                
                // Create basic runtime animation as fallback
                var basicWave = ScriptableObject.CreateInstance<WaveAnimation>();
                basicWave.animationName = "Basic Wave";
                basicWave.primaryColor = Color.red;
                basicWave.speed = 1f;
                availableAnimations.Add(basicWave);
            }
        }
        
        /// <summary>
        /// Validate and fix animation index
        /// </summary>
        private void ValidateAnimationIndex()
        {
            if (availableAnimations.Count == 0)
            {
                currentAnimationIndex = 0;
                return;
            }
            
            if (currentAnimationIndex >= availableAnimations.Count)
            {
                currentAnimationIndex = availableAnimations.Count - 1;
            }
            if (currentAnimationIndex < 0)
            {
                currentAnimationIndex = 0;
            }
        }
        
        /// <summary>
        /// Handle animation index change
        /// </summary>
        private void OnAnimationChanged()
        {
            ValidateAnimationIndex();
            ResetAnimation();
            
            string animationName = CurrentAnimationName;
            LEDVisualizationEvents.TriggerAnimationChanged(animationName);
            
            // Restart continuous animation with new animation type
            if (autoPlayOnStart && graphManager != null && graphManager.DataLoaded)
            {
                // Stop current animation
                continuousAnimationRunning = false;
                
                // Start with new animation
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    StartContinuousAnimationInternal();
                };
                #endif
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"🎬 Animation changed to: {animationName} (Index: {currentAnimationIndex})");
            }
        }
    }
}
