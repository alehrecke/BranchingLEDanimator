using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Spatial panning animation that uses the 3D positions of physical LED pixels.
    /// Creates rainbow or gradient effects that pan across the structure based on 
    /// spatial position (height, horizontal, depth, or custom direction).
    /// 
    /// Works directly with mapped LED positions from the circuit mapper,
    /// not the imported graph nodes.
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialPanAnimation", menuName = "LED Animations/LED-Based/Spatial Pan")]
    public class SpatialPanAnimation : LEDAnimationType
    {
        public enum PanDirection
        {
            Vertical,      // Y axis (up/down)
            Horizontal,    // X axis (left/right)
            Depth,         // Z axis (front/back)
            Diagonal,      // Combined X and Y
            Radial,        // Distance from center
            Custom         // User-defined axis
        }
        
        public enum ColorMode
        {
            Rainbow,           // Full HSV rainbow
            TwoColorGradient,  // Primary to Secondary
            ThreeColorGradient // Primary -> Secondary -> Tertiary
        }
        
        public enum PositionMode
        {
            Spatial3D,         // Use actual 3D positions (can be jumpy on curved wires)
            StripProgress,     // Use linear position along each strip (smooth but ignores 3D)
            GlobalLEDIndex,    // Use global LED index (smooth continuous gradient)
            BlendSpatialStrip  // Blend between 3D position and strip progress
        }
        
        [Header("Position Mode")]
        [Tooltip("How to calculate each LED's position in the gradient")]
        public PositionMode positionMode = PositionMode.Spatial3D;
        
        [Tooltip("Blend factor when using BlendSpatialStrip (0 = pure spatial, 1 = pure strip progress)")]
        [Range(0f, 1f)]
        public float spatialStripBlend = 0.5f;
        
        [Header("Pan Direction (for Spatial3D mode)")]
        public PanDirection panDirection = PanDirection.Vertical;
        
        [Tooltip("Custom direction vector (only used when panDirection is Custom)")]
        public Vector3 customDirection = Vector3.up;
        
        [Tooltip("Reverse the pan direction")]
        public bool reversePan = false;
        
        [Header("Pan Speed & Scale")]
        [Tooltip("How fast the gradient pans (cycles per second)")]
        [Range(0.01f, 5f)]
        public float panSpeed = 0.5f;
        
        [Tooltip("How many times the gradient repeats across the structure")]
        [Range(0.1f, 10f)]
        public float gradientRepeats = 1f;
        
        [Tooltip("Offset the starting position of the gradient")]
        [Range(0f, 1f)]
        public float gradientOffset = 0f;
        
        [Header("Color Settings")]
        public ColorMode colorMode = ColorMode.Rainbow;
        
        [Tooltip("Third color for three-color gradient mode")]
        public Color tertiaryColor = Color.cyan;
        
        [Header("Rainbow Settings")]
        [Tooltip("Starting hue for rainbow (0-1)")]
        [Range(0f, 1f)]
        public float rainbowHueStart = 0f;
        
        [Tooltip("Hue range for rainbow (0-1, where 1 = full spectrum)")]
        [Range(0f, 1f)]
        public float rainbowHueRange = 1f;
        
        [Header("Pulse Mode (Optional)")]
        [Tooltip("Enable pulsing brightness")]
        public bool enablePulse = false;
        
        [Tooltip("Pulse frequency (pulses per pan cycle)")]
        [Range(1f, 20f)]
        public float pulseFrequency = 4f;
        
        [Tooltip("Minimum brightness during pulse")]
        [Range(0f, 1f)]
        public float pulseMinBrightness = 0.3f;
        
        [Header("Smoothing")]
        [Tooltip("Apply smoothstep to gradient transitions")]
        public bool smoothGradient = true;
        
        [Tooltip("Curve for custom gradient falloff")]
        public AnimationCurve gradientCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [Header("Debug")]
        [Tooltip("Log geometry bounds info on start")]
        public bool debugLogBounds = false;
        
        // Cached bounds
        private Vector3 boundsMin;
        private Vector3 boundsMax;
        private Vector3 boundsCenter;
        private int lastLEDCount = 0;
        private float lastDebugTime = 0f;
        private PanDirection lastLoggedDirection = (PanDirection)(-1);
        
        // LED mode disabled - node mode fallback works more reliably
        // TODO: Debug LED mode mapping issues before re-enabling
        public override bool SupportsLEDMode => false;
        
        public override Color[] CalculateLEDColors(MappedGraph mappedGraph, float time, int frame)
        {
            if (mappedGraph == null || !mappedGraph.IsValid)
                return null;
            
            int ledCount = mappedGraph.TotalLEDCount;
            Color[] colors = new Color[ledCount];
            
            // Recalculate bounds when LED count changes (new geometry loaded)
            if (ledCount != lastLEDCount)
            {
                CalculateBounds(mappedGraph);
                lastLEDCount = ledCount;
                
                if (debugLogBounds)
                {
                    Vector3 size = boundsMax - boundsMin;
                    Debug.Log($"[SpatialPan] Geometry bounds - Min: {boundsMin}, Max: {boundsMax}");
                    Debug.Log($"[SpatialPan] Size - X: {size.x:F2}, Y: {size.y:F2}, Z: {size.z:F2}");
                    Debug.Log($"[SpatialPan] Current direction: {panDirection}");
                    
                    // Log sample LED positions
                    if (ledCount > 0)
                    {
                        int sampleIdx = ledCount / 2;
                        Vector3 samplePos = mappedGraph.LEDPositions[sampleIdx];
                        float sampleNorm = GetNormalizedPosition(samplePos);
                        Debug.Log($"[SpatialPan] Sample LED {sampleIdx} pos: {samplePos}, normalized: {sampleNorm:F3}");
                    }
                }
            }
            
            // Log when direction changes during runtime
            if (debugLogBounds && panDirection != lastLoggedDirection)
            {
                Debug.Log($"[SpatialPan] Direction changed to: {panDirection}");
                lastLoggedDirection = panDirection;
                
                // Show range of normalized values for current direction
                float minNorm = float.MaxValue;
                float maxNorm = float.MinValue;
                for (int i = 0; i < Mathf.Min(ledCount, 100); i++)
                {
                    float norm = GetNormalizedPosition(mappedGraph.LEDPositions[i]);
                    minNorm = Mathf.Min(minNorm, norm);
                    maxNorm = Mathf.Max(maxNorm, norm);
                }
                Debug.Log($"[SpatialPan] Normalized range for {panDirection}: {minNorm:F3} to {maxNorm:F3}");
            }
            
            // Animation time - reverse direction if needed
            float animTime = time * speed * panSpeed;
            if (reversePan)
            {
                animTime = -animTime;
            }
            
            // Process each LED
            for (int i = 0; i < ledCount; i++)
            {
                // Get normalized position based on selected mode
                float normalizedPos = GetLEDNormalizedPosition(mappedGraph, i);
                
                // Apply gradient repeats and animation
                float gradientPos = normalizedPos * gradientRepeats + animTime + gradientOffset;
                
                // Keep in 0-1 range (wrapping)
                gradientPos = gradientPos - Mathf.Floor(gradientPos);
                
                // Apply smoothing curve
                if (smoothGradient)
                {
                    gradientPos = gradientCurve.Evaluate(gradientPos);
                }
                
                // Get color based on mode
                Color ledColor = GetGradientColor(gradientPos);
                
                // Apply pulse if enabled
                if (enablePulse)
                {
                    float pulsePhase = (gradientPos * pulseFrequency + animTime * pulseFrequency) * Mathf.PI * 2f;
                    float pulseBrightness = Mathf.Lerp(pulseMinBrightness, 1f, (Mathf.Sin(pulsePhase) + 1f) * 0.5f);
                    ledColor *= pulseBrightness;
                }
                
                colors[i] = ledColor;
            }
            
            return colors;
        }
        
        /// <summary>
        /// Get normalized position for an LED based on the selected position mode
        /// </summary>
        private float GetLEDNormalizedPosition(MappedGraph mappedGraph, int ledIndex)
        {
            switch (positionMode)
            {
                case PositionMode.Spatial3D:
                    // Use 3D world position
                    return GetNormalizedPosition(mappedGraph.LEDPositions[ledIndex]);
                    
                case PositionMode.StripProgress:
                    // Use linear progress along each strip (0 to 1 within strip)
                    // This creates a smooth gradient per strip
                    return mappedGraph.LEDProgress[ledIndex];
                    
                case PositionMode.GlobalLEDIndex:
                    // Use global LED index for continuous gradient across all LEDs
                    return (float)ledIndex / Mathf.Max(1, mappedGraph.TotalLEDCount - 1);
                    
                case PositionMode.BlendSpatialStrip:
                    // Blend between spatial position and strip progress
                    float spatial = GetNormalizedPosition(mappedGraph.LEDPositions[ledIndex]);
                    float stripProg = mappedGraph.LEDProgress[ledIndex];
                    return Mathf.Lerp(spatial, stripProg, spatialStripBlend);
                    
                default:
                    return GetNormalizedPosition(mappedGraph.LEDPositions[ledIndex]);
            }
        }
        
        private void CalculateBounds(MappedGraph mappedGraph)
        {
            if (mappedGraph.LEDPositions.Count == 0) return;
            
            boundsMin = mappedGraph.LEDPositions[0];
            boundsMax = mappedGraph.LEDPositions[0];
            
            foreach (var pos in mappedGraph.LEDPositions)
            {
                boundsMin = Vector3.Min(boundsMin, pos);
                boundsMax = Vector3.Max(boundsMax, pos);
            }
            
            boundsCenter = (boundsMin + boundsMax) * 0.5f;
        }
        
        private float GetNormalizedPosition(Vector3 pos)
        {
            Vector3 size = boundsMax - boundsMin;
            
            switch (panDirection)
            {
                case PanDirection.Vertical:
                    if (size.y < 0.01f)
                    {
                        // Geometry is flat in Y - fall back to using X or Z
                        if (size.x > size.z && size.x > 0.01f)
                            return (pos.x - boundsMin.x) / size.x;
                        else if (size.z > 0.01f)
                            return (pos.z - boundsMin.z) / size.z;
                        return 0.5f;
                    }
                    return (pos.y - boundsMin.y) / size.y;
                    
                case PanDirection.Horizontal:
                    if (size.x < 0.01f)
                    {
                        if (size.z > size.y && size.z > 0.01f)
                            return (pos.z - boundsMin.z) / size.z;
                        else if (size.y > 0.01f)
                            return (pos.y - boundsMin.y) / size.y;
                        return 0.5f;
                    }
                    return (pos.x - boundsMin.x) / size.x;
                    
                case PanDirection.Depth:
                    if (size.z < 0.01f)
                    {
                        if (size.x > size.y && size.x > 0.01f)
                            return (pos.x - boundsMin.x) / size.x;
                        else if (size.y > 0.01f)
                            return (pos.y - boundsMin.y) / size.y;
                        return 0.5f;
                    }
                    return (pos.z - boundsMin.z) / size.z;
                    
                case PanDirection.Diagonal:
                    // Combined X and Y for diagonal sweep
                    float diagX = size.x > 0.001f ? (pos.x - boundsMin.x) / size.x : 0.5f;
                    float diagY = size.y > 0.001f ? (pos.y - boundsMin.y) / size.y : 0.5f;
                    return (diagX + diagY) * 0.5f;
                    
                case PanDirection.Radial:
                    // Distance from center
                    float maxDist = Vector3.Distance(boundsMin, boundsCenter);
                    if (maxDist < 0.001f) return 0.5f;
                    float dist = Vector3.Distance(pos, boundsCenter);
                    return Mathf.Clamp01(dist / maxDist);
                    
                case PanDirection.Custom:
                    // Project onto custom direction
                    Vector3 dir = customDirection.normalized;
                    if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;
                    
                    // Calculate extent along custom direction
                    float minProj = float.MaxValue;
                    float maxProj = float.MinValue;
                    float currentProj = Vector3.Dot(pos, dir);
                    
                    // Use bounds corners to find extent
                    Vector3[] corners = new Vector3[]
                    {
                        boundsMin,
                        boundsMax,
                        new Vector3(boundsMin.x, boundsMin.y, boundsMax.z),
                        new Vector3(boundsMin.x, boundsMax.y, boundsMin.z),
                        new Vector3(boundsMax.x, boundsMin.y, boundsMin.z),
                        new Vector3(boundsMin.x, boundsMax.y, boundsMax.z),
                        new Vector3(boundsMax.x, boundsMin.y, boundsMax.z),
                        new Vector3(boundsMax.x, boundsMax.y, boundsMin.z)
                    };
                    
                    foreach (var corner in corners)
                    {
                        float proj = Vector3.Dot(corner, dir);
                        minProj = Mathf.Min(minProj, proj);
                        maxProj = Mathf.Max(maxProj, proj);
                    }
                    
                    if (maxProj - minProj < 0.001f) return 0.5f;
                    return (currentProj - minProj) / (maxProj - minProj);
                    
                default:
                    return 0.5f;
            }
        }
        
        private Color GetGradientColor(float t)
        {
            switch (colorMode)
            {
                case ColorMode.Rainbow:
                    // Full rainbow using HSV
                    float hue = rainbowHueStart + t * rainbowHueRange;
                    hue = hue - Mathf.Floor(hue); // Wrap hue
                    return Color.HSVToRGB(hue, saturation, brightness);
                    
                case ColorMode.TwoColorGradient:
                    // Simple lerp between primary and secondary
                    return Color.Lerp(primaryColor, secondaryColor, t);
                    
                case ColorMode.ThreeColorGradient:
                    // Three color gradient: primary -> secondary -> tertiary
                    if (t < 0.5f)
                    {
                        return Color.Lerp(primaryColor, secondaryColor, t * 2f);
                    }
                    else
                    {
                        return Color.Lerp(secondaryColor, tertiaryColor, (t - 0.5f) * 2f);
                    }
                    
                default:
                    return primaryColor;
            }
        }
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            // Fallback for non-LED mode (should rarely be used)
            int nodeCount = nodePositions.Count;
            Color[] colors = new Color[nodeCount];
            
            // Calculate bounds
            Vector3 min = nodePositions[0];
            Vector3 max = nodePositions[0];
            foreach (var pos in nodePositions)
            {
                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, pos);
            }
            
            float animTime = time * speed * panSpeed;
            if (reversePan)
            {
                animTime = -animTime;
            }
            
            for (int i = 0; i < nodeCount; i++)
            {
                Vector3 pos = nodePositions[i];
                Vector3 size = max - min;
                
                // Default to vertical pan for fallback
                float normalizedPos = size.y > 0.001f ? (pos.y - min.y) / size.y : 0.5f;
                float gradientPos = normalizedPos * gradientRepeats + animTime + gradientOffset;
                gradientPos = gradientPos - Mathf.Floor(gradientPos);
                
                colors[i] = GetGradientColor(gradientPos);
            }
            
            return colors;
        }
        
        private void OnValidate()
        {
            // Reset cache when parameters change - forces recalculation
            lastLEDCount = 0;
            
            // Ensure custom direction is valid
            if (customDirection.sqrMagnitude < 0.001f)
            {
                customDirection = Vector3.up;
            }
        }
    }
}
