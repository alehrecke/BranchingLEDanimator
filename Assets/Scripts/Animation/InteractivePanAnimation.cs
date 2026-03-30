using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Interactive panning animation where the pan direction is controlled by player touch.
    /// The pan vector originates from the center of all endpoints (on the floor) and points
    /// towards the touched endpoint(s). Multiple touches average the direction.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractivePanAnimation", menuName = "LED Animations/Interactive/Interactive Pan")]
    public class InteractivePanAnimation : LEDAnimationType
    {
        public enum ColorMode { Rainbow, TwoColorGradient, ThreeColorGradient }
        
        [Header("Pan Speed & Scale")]
        [Tooltip("Speed of the gradient animation")]
        [Range(0.01f, 5f)]
        public float panSpeed = 0.5f;
        
        [Tooltip("How many times the gradient repeats across the geometry")]
        [Range(0.1f, 10f)]
        public float gradientRepeats = 1f;
        
        [Tooltip("Offset the gradient start position")]
        [Range(0f, 1f)]
        public float gradientOffset = 0f;
        
        [Header("Color Settings")]
        public ColorMode colorMode = ColorMode.Rainbow;
        
        [Header("Rainbow Settings")]
        [Tooltip("Starting hue for rainbow (0=red, 0.33=green, 0.66=blue)")]
        [Range(0f, 1f)]
        public float rainbowHueStart = 0f;
        
        [Tooltip("How much of the color spectrum to use (1 = full rainbow)")]
        [Range(0.1f, 1f)]
        public float rainbowHueRange = 1f;
        
        [Tooltip("Color saturation (0 = grayscale, 1 = vivid)")]
        [Range(0f, 1f)]
        public float rainbowSaturation = 1f;
        
        [Tooltip("Color brightness/value")]
        [Range(0f, 1f)]
        public float rainbowBrightness = 1f;
        
        [Header("Gradient Colors (for non-rainbow modes)")]
        public Color tertiaryColor = Color.green;
        
        [Header("Smoothing")]
        [Tooltip("Apply smoothstep to gradient transitions")]
        public bool smoothGradient = false;
        
        [Tooltip("Curve for custom gradient shape")]
        public AnimationCurve gradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Header("Pulse Mode (Optional)")]
        [Tooltip("Add a pulsing brightness effect")]
        public bool enablePulse = false;
        
        [Range(0.5f, 10f)]
        public float pulseFrequency = 2f;
        
        [Range(0f, 1f)]
        public float pulseMinBrightness = 0.3f;
        
        [Header("Direction Smoothing")]
        [Tooltip("How quickly the pan direction responds to touch changes")]
        [Range(0.1f, 20f)]
        public float directionSmoothSpeed = 5f;
        
        [Tooltip("Default pan direction when nothing is touched")]
        public Vector3 defaultDirection = Vector3.up;
        
        [Header("Debug Visualization")]
        [Tooltip("Draw the pan direction vector in Scene/Game view")]
        public bool showDirectionVector = true;
        
        [Tooltip("Length of the direction arrow")]
        [Range(1f, 50f)]
        public float vectorDisplayLength = 10f;
        
        [Tooltip("Color of the direction arrow")]
        public Color vectorColor = Color.yellow;
        
        [Header("Visual Feedback")]
        [Tooltip("Brighten touched endpoints")]
        public bool highlightTouchedEndpoints = true;
        
        [Range(0f, 1f)]
        public float endpointHighlightIntensity = 0.5f;
        
        [Range(1f, 10f)]
        public float endpointHighlightRadius = 3f;
        
        public enum InputMode { PlayerController, None }
        
        [Header("Input")]
        [SerializeField] private InputMode inputMode = InputMode.PlayerController;
        
        // State
        private List<Vector3> cachedPositions;
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private Vector3 centerPoint;
        private Vector3 currentDirection;
        private Vector3 targetDirection;
        private HashSet<int> activeEndpoints = new HashSet<int>();
        private bool initialized = false;
        private float minHeight, maxHeight;
        
        // Bounds for normalization
        private Vector3 boundsMin, boundsMax, boundsCenter;
        
        void OnEnable()
        {
            initialized = false;
            activeEndpoints.Clear();
            currentDirection = defaultDirection.normalized;
            targetDirection = currentDirection;
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
            
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            float currentTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(Time.deltaTime, 0.001f, 0.1f);
            
            if (!initialized)
            {
                Initialize(nodePositions, edgeConnections);
            }
            
            // Process input
            ProcessInput();
            
            // Update direction smoothly
            UpdateDirection(deltaTime);
            
            // Draw debug visualization
            if (showDirectionVector)
            {
                DrawDirectionVector();
            }
            
            // Calculate animation
            float animTime = time * speed * panSpeed;
            
            // Render the gradient based on current direction
            for (int i = 0; i < nodePositions.Count; i++)
            {
                float normalizedPos = GetNormalizedPosition(nodePositions[i]);
                float gradientPos = normalizedPos * gradientRepeats + animTime + gradientOffset;
                gradientPos = gradientPos - Mathf.Floor(gradientPos); // Wrap to 0-1
                
                colors[i] = GetGradientColor(gradientPos, animTime);
            }
            
            // Highlight touched endpoints
            if (highlightTouchedEndpoints && activeEndpoints.Count > 0)
            {
                foreach (int endpoint in activeEndpoints)
                {
                    if (endpoint >= nodePositions.Count) continue;
                    Vector3 endpointPos = nodePositions[endpoint];
                    
                    // Highlight nodes near the touched endpoint
                    for (int i = 0; i < nodePositions.Count; i++)
                    {
                        float dist = Vector3.Distance(nodePositions[i], endpointPos);
                        if (dist < endpointHighlightRadius)
                        {
                            float intensity = 1f - (dist / endpointHighlightRadius);
                            intensity *= endpointHighlightIntensity;
                            colors[i] = Color.Lerp(colors[i], Color.white, intensity);
                        }
                    }
                }
            }
            
            return colors;
        }
        
        private void Initialize(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Calculate bounds
            boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
            foreach (var pos in nodePositions)
            {
                boundsMin = Vector3.Min(boundsMin, pos);
                boundsMax = Vector3.Max(boundsMax, pos);
            }
            boundsCenter = (boundsMin + boundsMax) * 0.5f;
            minHeight = boundsMin.y;
            maxHeight = boundsMax.y;
            
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
            
            // Find endpoints (nodes with only one connection)
            endpointNodes.Clear();
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    endpointNodes.Add(kvp.Key);
                }
            }
            
            // Calculate center point from endpoints (projected to floor)
            if (endpointNodes.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (int endpoint in endpointNodes)
                {
                    if (endpoint < nodePositions.Count)
                    {
                        sum += nodePositions[endpoint];
                    }
                }
                centerPoint = sum / endpointNodes.Count;
                centerPoint.y = minHeight; // Project to floor level
            }
            else
            {
                centerPoint = boundsCenter;
                centerPoint.y = minHeight;
            }
            
            // Subscribe to input
            SubscribeToInput();
            
            currentDirection = defaultDirection.normalized;
            targetDirection = currentDirection;
            
            initialized = true;
        }
        
        private void SubscribeToInput()
        {
            if (inputMode == InputMode.PlayerController)
            {
                GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
                GraphPlayerController.OnEndpointReleased -= OnEndpointReleased;
                GraphPlayerController.OnEndpointPressed += OnEndpointPressed;
                GraphPlayerController.OnEndpointReleased += OnEndpointReleased;
            }
        }
        
        private void OnEndpointPressed(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            if (OwnerGraphManager != null && source != OwnerGraphManager) return;
            activeEndpoints.Add(endpointIndex);
            UpdateTargetDirection();
        }
        
        private void OnEndpointReleased(LEDGraphManager source, int endpointIndex, Vector3 position)
        {
            if (OwnerGraphManager != null && source != OwnerGraphManager) return;
            activeEndpoints.Remove(endpointIndex);
            UpdateTargetDirection();
        }
        
        private void ProcessInput()
        {
            // Input is handled via events, but we could add mouse click support here
        }
        
        private void UpdateTargetDirection()
        {
            if (activeEndpoints.Count == 0)
            {
                // No touch - return to default direction
                targetDirection = defaultDirection.normalized;
                return;
            }
            
            // Calculate average direction from center to all touched endpoints
            Vector3 avgDirection = Vector3.zero;
            int validCount = 0;
            
            foreach (int endpoint in activeEndpoints)
            {
                if (endpoint < cachedPositions.Count)
                {
                    Vector3 endpointPos = cachedPositions[endpoint];
                    Vector3 dirToEndpoint = endpointPos - centerPoint;
                    
                    // Normalize to prevent closer endpoints from having less influence
                    if (dirToEndpoint.magnitude > 0.01f)
                    {
                        avgDirection += dirToEndpoint.normalized;
                        validCount++;
                    }
                }
            }
            
            if (validCount > 0)
            {
                avgDirection /= validCount;
                if (avgDirection.magnitude > 0.01f)
                {
                    targetDirection = avgDirection.normalized;
                }
            }
        }
        
        private void UpdateDirection(float deltaTime)
        {
            // Smoothly interpolate current direction towards target
            currentDirection = Vector3.Lerp(currentDirection, targetDirection, 
                directionSmoothSpeed * deltaTime);
            
            // Ensure it stays normalized
            if (currentDirection.magnitude > 0.01f)
            {
                currentDirection = currentDirection.normalized;
            }
        }
        
        private void DrawDirectionVector()
        {
            // Draw the main direction arrow
            Vector3 arrowEnd = centerPoint + currentDirection * vectorDisplayLength;
            Debug.DrawLine(centerPoint, arrowEnd, vectorColor);
            
            // Draw arrowhead
            Vector3 right = Vector3.Cross(currentDirection, Vector3.up).normalized;
            if (right.magnitude < 0.1f)
            {
                right = Vector3.Cross(currentDirection, Vector3.forward).normalized;
            }
            Vector3 up = Vector3.Cross(right, currentDirection).normalized;
            
            float arrowHeadSize = vectorDisplayLength * 0.15f;
            Vector3 arrowBase = arrowEnd - currentDirection * arrowHeadSize;
            
            Debug.DrawLine(arrowEnd, arrowBase + right * arrowHeadSize * 0.5f, vectorColor);
            Debug.DrawLine(arrowEnd, arrowBase - right * arrowHeadSize * 0.5f, vectorColor);
            Debug.DrawLine(arrowEnd, arrowBase + up * arrowHeadSize * 0.5f, vectorColor);
            Debug.DrawLine(arrowEnd, arrowBase - up * arrowHeadSize * 0.5f, vectorColor);
            
            // Draw center point marker (small cross)
            float markerSize = 1f;
            Debug.DrawLine(centerPoint - Vector3.right * markerSize, centerPoint + Vector3.right * markerSize, Color.white);
            Debug.DrawLine(centerPoint - Vector3.forward * markerSize, centerPoint + Vector3.forward * markerSize, Color.white);
            Debug.DrawLine(centerPoint - Vector3.up * markerSize, centerPoint + Vector3.up * markerSize, Color.white);
            
            // Draw target direction (if different from current) in a dimmer color
            if (Vector3.Angle(currentDirection, targetDirection) > 1f)
            {
                Color targetColor = vectorColor * 0.4f;
                Vector3 targetEnd = centerPoint + targetDirection * vectorDisplayLength * 0.8f;
                Debug.DrawLine(centerPoint, targetEnd, targetColor);
            }
            
            // Draw lines to active endpoints
            if (activeEndpoints.Count > 0 && cachedPositions != null)
            {
                foreach (int endpoint in activeEndpoints)
                {
                    if (endpoint < cachedPositions.Count)
                    {
                        Debug.DrawLine(centerPoint, cachedPositions[endpoint], Color.cyan);
                    }
                }
            }
        }
        
        private float GetNormalizedPosition(Vector3 pos)
        {
            // Project position onto the current pan direction
            Vector3 relativePos = pos - centerPoint;
            float projection = Vector3.Dot(relativePos, currentDirection);
            
            // Normalize based on geometry size
            Vector3 size = boundsMax - boundsMin;
            float maxExtent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            
            if (maxExtent < 0.01f) return 0.5f;
            
            // Map to 0-1 range
            float normalized = (projection / maxExtent) + 0.5f;
            return Mathf.Clamp01(normalized);
        }
        
        private Color GetGradientColor(float t, float animTime = 0f)
        {
            // Apply smoothing curve if enabled
            if (smoothGradient)
            {
                t = gradientCurve.Evaluate(t);
            }
            
            Color color;
            switch (colorMode)
            {
                case ColorMode.Rainbow:
                    float hue = rainbowHueStart + t * rainbowHueRange;
                    hue = hue - Mathf.Floor(hue); // Wrap
                    color = Color.HSVToRGB(hue, rainbowSaturation, rainbowBrightness);
                    break;
                    
                case ColorMode.TwoColorGradient:
                    color = Color.Lerp(primaryColor, secondaryColor, t);
                    break;
                    
                case ColorMode.ThreeColorGradient:
                    if (t < 0.5f)
                        color = Color.Lerp(primaryColor, secondaryColor, t * 2f);
                    else
                        color = Color.Lerp(secondaryColor, tertiaryColor, (t - 0.5f) * 2f);
                    break;
                    
                default:
                    color = primaryColor;
                    break;
            }
            
            // Apply pulse if enabled
            if (enablePulse)
            {
                float pulsePhase = (t * pulseFrequency + animTime * pulseFrequency) * Mathf.PI * 2f;
                float pulseBrightness = Mathf.Lerp(pulseMinBrightness, 1f, (Mathf.Sin(pulsePhase) + 1f) * 0.5f);
                color *= pulseBrightness;
            }
            
            return color;
        }
        
        /// <summary>
        /// Simulate touching an endpoint (for external triggering)
        /// </summary>
        public void SimulateTouchEndpoint(int endpointIndex)
        {
            if (endpointNodes.Contains(endpointIndex) && cachedPositions != null)
            {
                Vector3 pos = endpointIndex < cachedPositions.Count ? cachedPositions[endpointIndex] : Vector3.zero;
                var gm = Object.FindFirstObjectByType<LEDGraphManager>();
                if (gm != null)
                    GraphPlayerController.SimulatePress(gm, endpointIndex, pos);
                else
                    OnEndpointPressed(null, endpointIndex, pos);
            }
        }
        
        /// <summary>
        /// Simulate releasing an endpoint (for external triggering)
        /// </summary>
        public void SimulateReleaseEndpoint(int endpointIndex)
        {
            if (cachedPositions != null)
            {
                Vector3 pos = endpointIndex < cachedPositions.Count ? cachedPositions[endpointIndex] : Vector3.zero;
                var gm = Object.FindFirstObjectByType<LEDGraphManager>();
                if (gm != null)
                    GraphPlayerController.SimulateRelease(gm, endpointIndex, pos);
                else
                    OnEndpointReleased(null, endpointIndex, pos);
            }
        }
        
        /// <summary>
        /// Get the current pan direction (for debugging/visualization)
        /// </summary>
        public Vector3 CurrentPanDirection => currentDirection;
        
        /// <summary>
        /// Get the center point (for debugging/visualization)
        /// </summary>
        public Vector3 CenterPoint => centerPoint;
        
        /// <summary>
        /// Get number of active touches
        /// </summary>
        public int ActiveTouchCount => activeEndpoints.Count;
        
        private void OnDisable()
        {
            GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
            GraphPlayerController.OnEndpointReleased -= OnEndpointReleased;
            activeEndpoints.Clear();
        }
    }
}