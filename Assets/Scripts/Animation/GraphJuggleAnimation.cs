using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Player;
using BranchingLEDAnimator.Core;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Interactive juggling game with Eno/Bloom-inspired ambient audio
    /// Soft ambient bed with delicate xylophone-like tones on interactions
    /// </summary>
    [CreateAssetMenu(fileName = "GraphJuggleAnimation", menuName = "LED Animations/Interactive/Graph Juggle")]
    public class GraphJuggleAnimation : LEDAnimationType
    {
        [Header("Game Settings")]
        [Tooltip("Base speed of the light ball (higher = faster)")]
        [Range(1f, 30f)]
        [SerializeField] private float pulseSpeed = 8f;
        
        [Tooltip("Time window to catch the ball at the endpoint")]
        [Range(0.1f, 2f)]
        [SerializeField] private float catchWindowSeconds = 0.5f;
        
        [SerializeField] private int startingLives = 3;
        [SerializeField] private bool autoStartPulse = true;
        
        [Header("Multi-Ball Settings")]
        [Tooltip("Maximum simultaneous balls in play (1-3 recommended)")]
        [Range(1, 5)]
        [SerializeField] private int maxSimultaneousBalls = 1;
        
        [Tooltip("Time between spawning additional balls (seconds)")]
        [Range(0.5f, 10f)]
        [SerializeField] private float ballSpawnInterval = 2f;
        
        [Header("Momentum System (Speed Intensifies with Catches)")]
        [Tooltip("Enable speed increase with consecutive catches")]
        [SerializeField] private bool enableMomentum = true;
        
        [Tooltip("Speed multiplier per catch (1.05 = 5% faster each catch)")]
        [Range(1.0f, 1.3f)]
        [SerializeField] private float momentumGainPerCatch = 1.08f;
        
        [Tooltip("Maximum speed multiplier (caps how fast it can get)")]
        [Range(1f, 3f)]
        [SerializeField] private float maxMomentumMultiplier = 2f;
        
        [Tooltip("Momentum decay rate when not catching (per second)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float momentumDecayRate = 0.05f;
        
        [Tooltip("Momentum lost on miss (0.3 = lose 30% of momentum)")]
        [Range(0f, 1f)]
        [SerializeField] private float momentumLossOnMiss = 0.3f;
        
        [Header("Input Mode")]
        [Tooltip("None = watch only (for simulation), MouseClick = mouse input, PlayerTouch = player proximity, Both = all inputs")]
        [SerializeField] private InputMode inputMode = InputMode.Both;
        public enum InputMode { None, MouseClick, PlayerTouch, Both }
        
        [Header("Ambient Audio")]
        [Tooltip("Base volume of the ambient drone")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientVolume = 0.5f;
        [Tooltip("How much the ambient pulses with light movement (0 = steady, 1 = dramatic)")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientPulseAmount = 0.6f;
        [Tooltip("Ambient drone base frequency (Hz)")]
        [SerializeField] private float ambientFrequency = 55f;
        [Tooltip("Second drone frequency for richness")]
        [SerializeField] private float ambientFrequency2 = 82.5f; // Perfect fifth above
        [Tooltip("How much filtered noise in the ambient")]
        [Range(0f, 0.5f)]
        [SerializeField] private float ambientNoiseAmount = 0.15f;
        
        [Header("Bell Tone Envelope (ADSR)")]
        [Tooltip("Attack time in seconds")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float toneAttack = 0.01f;
        [Tooltip("Decay time in seconds")]
        [Range(0.1f, 2f)]
        [SerializeField] private float toneDecayTime = 0.8f;
        [Tooltip("Sustain level (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float toneSustain = 0.3f;
        [Tooltip("Release time in seconds")]
        [Range(0.5f, 5f)]
        [SerializeField] private float toneRelease = 2.0f;
        
        [Header("Bell Tone Settings")]
        [Tooltip("Base frequency for the lowest leg (lower = warmer)")]
        [SerializeField] private float baseFrequency = 196f; // G3 - warm and low
        [Tooltip("Volume of bell-like event tones")]
        [Range(0f, 1f)]
        [SerializeField] private float toneVolume = 0.4f;
        [Tooltip("How much the second harmonic contributes")]
        [Range(0f, 0.5f)]
        [SerializeField] private float harmonic2 = 0.25f;
        [Tooltip("How much the third harmonic contributes")]
        [Range(0f, 0.3f)]
        [SerializeField] private float harmonic3 = 0.12f;
        
        [Header("Pulse Sound")]
        [Tooltip("Subtle presence of the traveling pulse (reserved for future use)")]
        #pragma warning disable CS0414
        [SerializeField] private float pulseAudioVolume = 0.08f;
        #pragma warning restore CS0414
        
        [Header("Visual Settings")]
        [SerializeField] private Color groundColor = new Color(1f, 0.6f, 0.3f);
        [SerializeField] private Color skyColor = new Color(0.4f, 0.7f, 1f);
        [SerializeField] private Color catchReadyColor = new Color(1f, 1f, 0.7f);
        
        [Tooltip("Width of the light trail (number of LEDs)")]
        [Range(1f, 20f)]
        [SerializeField] private float pulseWidth = 4f;
        
        [Tooltip("Shape of the pulse intensity falloff")]
        [SerializeField] private AnimationCurve pulseShape = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("Ball Colors")]
        [Tooltip("Use rainbow colors evenly distributed among active balls")]
        [SerializeField] private bool useRainbowColors = true;
        
        [Tooltip("Saturation of rainbow colors")]
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowSaturation = 0.9f;
        
        [Tooltip("Base brightness of balls")]
        [Range(0.5f, 1f)]
        [SerializeField] private float rainbowBrightness = 1f;
        
        public enum PulseStyle { SymmetricGlow, VelocityTrail }
        
        [Header("Pulse Style")]
        [Tooltip("Symmetric: glows equally in both directions. Velocity: bright head with trailing fade")]
        [SerializeField] private PulseStyle pulseStyle = PulseStyle.VelocityTrail;
        
        [Tooltip("Length of trail in LED units (Velocity mode only)")]
        [Range(0f, 30f)]
        [SerializeField] private float trailLength = 12f;
        
        [Tooltip("Trail intensity at the start (near the ball)")]
        [Range(0f, 1f)]
        [SerializeField] private float trailIntensity = 0.6f;
        
        [Tooltip("Trail stretches with momentum (velocity effect)")]
        [SerializeField] private bool trailStretchWithMomentum = true;
        
        [Header("Feedback")]
        [SerializeField] private float successFlashDuration = 0.5f;
        [SerializeField] private float successBrightnessBoost = 1.8f;
        [SerializeField] private float catchBrightnessBoost = 1.3f;
        
        [Header("Game State (Read Only)")]
        [SerializeField] private int currentScore = 0;
        [SerializeField] private int currentLives = 3;
        [SerializeField] private int streak = 0;
        [SerializeField] private bool gameActive = false;
        
        // Pentatonic scale intervals (always harmonious)
        private static readonly int[] PentatonicIntervals = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21 };
        
        // Pulse tracking
        private class ActivePulse
        {
            public List<int> path;
            public float progress;
            public float lastProgress; // For velocity calculation
            public int targetEndpoint;
            public bool catchable;
            public bool caught;
            public float spawnTime;
            public float currentHeight;
            public float hue; // Unique color hue (0-1)
            public int ballIndex; // Index for consistent coloring
            
            public ActivePulse(List<int> nodePath, float time, float assignedHue, int index)
            {
                path = nodePath;
                progress = 0f;
                lastProgress = 0f;
                targetEndpoint = nodePath[nodePath.Count - 1];
                catchable = false;
                caught = false;
                spawnTime = time;
                currentHeight = 0f;
                hue = assignedHue;
                ballIndex = index;
            }
        }
        
        // Track ball indices for consistent coloring
        private int nextBallIndex = 0;
        
        // Graph analysis
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private Dictionary<int, float> endpointFrequencies = new Dictionary<int, float>();
        private List<Vector3> cachedPositions;
        private float minHeight, maxHeight;
        private bool analysisComplete = false;
        
        // Game state
        private List<ActivePulse> activePulses = new List<ActivePulse>();
        private List<SuccessRipple> successRipples = new List<SuccessRipple>();
        private float lastRealTime = 0f;
        private float brightnessMultiplier = 1f;
        
        // Success ripple class - emanates from caught endpoint
        private class SuccessRipple
        {
            public int endpointIndex;
            public float startTime;
            public float duration;
            public Color color;
            public float speed;
        }
        
        // Multi-ball and momentum
        private float lastBallSpawnTime = 0f;
        private float currentMomentum = 1f;
        private float timeSinceLastCatch = 0f;
        
        // Audio system - simplified PlayOneShot approach
        private GameObject audioContainer;
        private AudioSource toneSource;
        private AudioSource ambientSource;
        private int sampleRate = 44100;
        private bool audioInitialized = false;
        
        // Pre-generated clips
        private Dictionary<int, AudioClip> toneClips = new Dictionary<int, AudioClip>();
        private AudioClip ambientClip;
        private AudioClip failClip;
        
        // Real-time ambient modulation
        private float currentPulseIntensity = 0f;
        private float ambientTargetVolume = 0f;
        
        // Input
        private static GraphJuggleAnimation activeInstance;
        private Camera gameCamera;
        private float clickRadius = 5f;
        private bool subscribedToPlayerEvents = false;
        private Queue<int> pendingPlayerTouches = new Queue<int>();
        
        // Init tracking
        private bool initializedThisSession = false;
        private int lastSessionId = -1;
        
        #region LED Mode Support
        
        // LED mode enables smooth pixel-perfect rendering
        // Disabled for now - node mode works reliably, LED mapping needs debugging
        public override bool SupportsLEDMode => false;
        
        // Cached LED mapping data
        private MappedGraph cachedLEDGraph;
        private Dictionary<int, int> nodeToLEDEndpoint = new Dictionary<int, int>();
        private Dictionary<int, List<int>> stripLEDPaths = new Dictionary<int, List<int>>();
        private bool ledMappingInitialized = false;
        
        /// <summary>
        /// Calculate colors directly for LEDs - smooth pixel-perfect rendering
        /// </summary>
        public override Color[] CalculateLEDColors(MappedGraph mappedGraph, float time, int frame)
        {
            if (mappedGraph == null || !mappedGraph.IsValid)
                return null;
            
            // Initialize LED mapping if needed
            if (!ledMappingInitialized || cachedLEDGraph != mappedGraph)
            {
                InitializeLEDMapping(mappedGraph);
            }
            
            Color[] colors = new Color[mappedGraph.TotalLEDCount];
            
            // Initialize with inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Render LED endpoints (idle glow)
            RenderLEDEndpoints(colors, mappedGraph, time);
            
            // Render pulses in LED space
            RenderLEDPulses(colors, mappedGraph);
            
            // Apply visual effects (ripples, brightness)
            ApplyLEDVisualEffects(colors, mappedGraph);
            
            return colors;
        }
        
        /// <summary>
        /// Build mapping from node endpoints to LED endpoints
        /// </summary>
        private void InitializeLEDMapping(MappedGraph mappedGraph)
        {
            cachedLEDGraph = mappedGraph;
            nodeToLEDEndpoint.Clear();
            stripLEDPaths.Clear();
            
            // Map each NODE endpoint to its closest LED endpoint
            foreach (int nodeEndpoint in endpointNodes)
            {
                if (nodeEndpoint >= cachedPositions.Count) continue;
                
                Vector3 nodePos = cachedPositions[nodeEndpoint];
                int closestLED = -1;
                float closestDist = float.MaxValue;
                
                foreach (int ledEndpoint in mappedGraph.LEDEndpoints)
                {
                    if (ledEndpoint < 0 || ledEndpoint >= mappedGraph.LEDPositions.Count)
                        continue;
                    
                    float dist = Vector3.SqrMagnitude(nodePos - mappedGraph.LEDPositions[ledEndpoint]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestLED = ledEndpoint;
                    }
                }
                
                if (closestLED >= 0)
                {
                    nodeToLEDEndpoint[nodeEndpoint] = closestLED;
                }
            }
            
            // Build LED paths for each strip
            for (int stripIdx = 0; stripIdx < mappedGraph.StripCount; stripIdx++)
            {
                stripLEDPaths[stripIdx] = mappedGraph.GetLEDsForStrip(stripIdx);
            }
            
            ledMappingInitialized = true;
        }
        
        /// <summary>
        /// Render endpoint glow in LED space
        /// </summary>
        private void RenderLEDEndpoints(Color[] colors, MappedGraph mappedGraph, float time)
        {
            float idlePulse = Mathf.Sin(time * 0.5f) * 0.5f + 0.5f;
            
            foreach (int ledEndpoint in mappedGraph.LEDEndpoints)
            {
                if (ledEndpoint >= colors.Length) continue;
                if (colors[ledEndpoint].maxColorComponent > 0.5f) continue;
                
                // Get height-based color
                Vector3 ledPos = mappedGraph.LEDPositions[ledEndpoint];
                float height = GetNormalizedHeight(ledPos);
                Color endpointColor = GetColorForHeight(height);
                float intensity = 0.15f + idlePulse * 0.1f;
                
                colors[ledEndpoint] = Color.Lerp(inactiveColor, endpointColor, intensity);
            }
        }
        
        /// <summary>
        /// Render pulses in LED space for smooth animation
        /// </summary>
        private void RenderLEDPulses(Color[] colors, MappedGraph mappedGraph)
        {
            foreach (var pulse in activePulses)
            {
                if (pulse.path == null || pulse.path.Count < 2) continue;
                
                // Find which strip this pulse is on by checking its target endpoint
                int targetLED = -1;
                if (nodeToLEDEndpoint.TryGetValue(pulse.targetEndpoint, out targetLED))
                {
                    // Find the strip containing this endpoint
                    int stripIdx = mappedGraph.LEDToStripIndex[targetLED];
                    var ledPath = stripLEDPaths.ContainsKey(stripIdx) ? stripLEDPaths[stripIdx] : null;
                    
                    if (ledPath != null && ledPath.Count > 0)
                    {
                        RenderPulseOnLEDStrip(colors, mappedGraph, pulse, ledPath);
                    }
                }
            }
        }
        
        /// <summary>
        /// Render a single pulse on an LED strip with unique color and velocity trail
        /// </summary>
        private void RenderPulseOnLEDStrip(Color[] colors, MappedGraph mappedGraph, ActivePulse pulse, List<int> ledPath)
        {
            int ledCount = ledPath.Count;
            if (ledCount == 0) return;
            
            // Convert node progress to LED progress
            float ledPosition = pulse.progress * (ledCount - 1);
            
            // Determine direction based on source vs target
            int targetLED = nodeToLEDEndpoint.ContainsKey(pulse.targetEndpoint) 
                ? nodeToLEDEndpoint[pulse.targetEndpoint] : -1;
            
            bool targetAtEnd = targetLED >= 0 && ledPath.Count > 0 && targetLED == ledPath[ledPath.Count - 1];
            int direction = 1; // Direction the ball is traveling
            if (!targetAtEnd && targetLED == ledPath[0])
            {
                ledPosition = (ledCount - 1) - ledPosition;
                direction = -1;
            }
            
            // Get the ball's unique color
            Color ballColor;
            if (useRainbowColors)
            {
                ballColor = Color.HSVToRGB(pulse.hue, rainbowSaturation, rainbowBrightness);
            }
            else
            {
                // Fall back to height-based coloring
                Vector3 ledPos = mappedGraph.LEDPositions[Mathf.Clamp((int)ledPosition, 0, ledPath.Count - 1)];
                float height = GetNormalizedHeight(ledPos);
                ballColor = GetColorForHeight(height);
            }
            
            // Render based on selected style
            if (pulseStyle == PulseStyle.VelocityTrail)
            {
                // VELOCITY TRAIL MODE: Bright head with fading trail behind
                float effectiveTrailLength = trailLength;
                if (trailStretchWithMomentum && enableMomentum)
                {
                    effectiveTrailLength *= currentMomentum;
                }
                
                // Render the trail behind the ball
                for (int i = 0; i < ledCount; i++)
                {
                    float signedDist = (i - ledPosition) * direction;
                    
                    // Trail is behind the ball (negative signed distance)
                    if (signedDist < 0 && signedDist > -effectiveTrailLength)
                    {
                        int ledIdx = ledPath[i];
                        if (ledIdx >= colors.Length) continue;
                        
                        float trailPos = -signedDist / effectiveTrailLength; // 0 at ball, 1 at end of trail
                        float trailFade = (1f - trailPos) * trailIntensity;
                        
                        // Trail color fades and desaturates toward the tail
                        Color trailColor;
                        if (useRainbowColors)
                        {
                            float fadedSat = Mathf.Lerp(rainbowSaturation, rainbowSaturation * 0.5f, trailPos);
                            float fadedBright = Mathf.Lerp(rainbowBrightness, rainbowBrightness * 0.3f, trailPos);
                            trailColor = Color.HSVToRGB(pulse.hue, fadedSat, fadedBright);
                        }
                        else
                        {
                            trailColor = ballColor * (1f - trailPos * 0.7f);
                        }
                        
                        colors[ledIdx] = BlendMax(colors[ledIdx], trailColor, trailFade);
                    }
                }
                
                // Render compact bright head (smaller than symmetric mode)
                float headWidth = pulseWidth * 0.5f; // Smaller head for trail mode
                for (int i = 0; i < ledCount; i++)
                {
                    float distance = Mathf.Abs(i - ledPosition);
                    
                    if (distance <= headWidth)
                    {
                        int ledIdx = ledPath[i];
                        if (ledIdx >= colors.Length) continue;
                        
                        float intensity = pulseShape.Evaluate(1f - (distance / headWidth));
                        
                        Color renderColor = ballColor;
                        if (pulse.catchable)
                        {
                            renderColor = Color.Lerp(renderColor, catchReadyColor, 0.5f);
                            intensity *= catchBrightnessBoost;
                        }
                        
                        colors[ledIdx] = BlendMax(colors[ledIdx], renderColor, intensity);
                    }
                }
            }
            else
            {
                // SYMMETRIC GLOW MODE: Equal glow in both directions
                for (int i = 0; i < ledCount; i++)
                {
                    float distance = Mathf.Abs(i - ledPosition);
                    
                    if (distance <= pulseWidth)
                    {
                        int ledIdx = ledPath[i];
                        if (ledIdx >= colors.Length) continue;
                        
                        float intensity = pulseShape.Evaluate(1f - (distance / pulseWidth));
                        
                        Color renderColor = ballColor;
                        if (pulse.catchable)
                        {
                            renderColor = Color.Lerp(renderColor, catchReadyColor, 0.5f);
                            intensity *= catchBrightnessBoost;
                        }
                        
                        colors[ledIdx] = BlendMax(colors[ledIdx], renderColor, intensity);
                    }
                }
            }
            
            // Blink target LED when catchable
            if (pulse.catchable && targetLED >= 0 && targetLED < colors.Length)
            {
                float blink = Mathf.Sin(Time.realtimeSinceStartup * 12f) * 0.5f + 0.5f;
                colors[targetLED] = Color.Lerp(catchReadyColor, Color.white, blink);
            }
            
            // Store current progress for next frame's velocity calculation
            pulse.lastProgress = pulse.progress;
        }
        
        /// <summary>
        /// Apply visual effects to LED colors including success ripples
        /// </summary>
        private void ApplyLEDVisualEffects(Color[] colors, MappedGraph mappedGraph)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            // Render success ripples in LED space
            for (int r = successRipples.Count - 1; r >= 0; r--)
            {
                var ripple = successRipples[r];
                float elapsed = currentTime - ripple.startTime;
                
                if (elapsed >= ripple.duration)
                {
                    successRipples.RemoveAt(r);
                    continue;
                }
                
                float normalizedTime = elapsed / ripple.duration;
                float rippleProgressLEDs = elapsed * ripple.speed * 30f; // Convert to LED units
                float fadeOut = 1f - normalizedTime;
                
                RenderLEDRipple(colors, mappedGraph, ripple.endpointIndex, rippleProgressLEDs, ripple.color, fadeOut);
            }
            
            if (Mathf.Abs(brightnessMultiplier - 1f) > 0.01f)
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] *= brightnessMultiplier;
                }
            }
        }
        
        /// <summary>
        /// Render a ripple emanating from an endpoint in LED space
        /// </summary>
        private void RenderLEDRipple(Color[] colors, MappedGraph mappedGraph, int nodeEndpoint, float progressLEDs, Color rippleColor, float intensity)
        {
            // Find the LED endpoint corresponding to this node endpoint
            if (!nodeToLEDEndpoint.TryGetValue(nodeEndpoint, out int ledEndpoint)) return;
            
            float rippleWidthLEDs = pulseWidth * 60f; // Width in LED units
            
            // Find which strip this endpoint is on
            int targetStrip = -1;
            for (int s = 0; s < mappedGraph.StripCount; s++)
            {
                var stripLEDs = mappedGraph.GetLEDsForStrip(s);
                if (stripLEDs.Contains(ledEndpoint))
                {
                    targetStrip = s;
                    break;
                }
            }
            
            if (targetStrip < 0) return;
            
            // Find position of endpoint in the strip
            var strip = mappedGraph.GetLEDsForStrip(targetStrip);
            int endpointPosInStrip = strip.IndexOf(ledEndpoint);
            if (endpointPosInStrip < 0) return;
            
            // Render ripple along this strip emanating from endpoint
            for (int i = 0; i < strip.Count; i++)
            {
                int led = strip[i];
                if (led >= colors.Length) continue;
                
                float distFromEndpoint = Mathf.Abs(i - endpointPosInStrip);
                float distFromRippleFront = Mathf.Abs(distFromEndpoint - progressLEDs);
                
                if (distFromRippleFront < rippleWidthLEDs)
                {
                    float localIntensity = 1f - (distFromRippleFront / rippleWidthLEDs);
                    localIntensity = Mathf.Pow(localIntensity, 0.5f);
                    
                    float finalIntensity = localIntensity * intensity * 0.6f;
                    colors[led] = Color.Lerp(colors[led], rippleColor, finalIntensity);
                }
            }
        }
        
        /// <summary>
        /// Blend colors taking maximum intensity
        /// </summary>
        private Color BlendMax(Color existing, Color newColor, float intensity)
        {
            Color blended = Color.Lerp(Color.black, newColor, intensity);
            return new Color(
                Mathf.Max(existing.r, blended.r),
                Mathf.Max(existing.g, blended.g),
                Mathf.Max(existing.b, blended.b),
                Mathf.Max(existing.a, blended.a)
            );
        }
        
        #endregion
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            cachedPositions = nodePositions;
            
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Initialize
            int currentSessionId = Application.isPlaying ? Time.frameCount / 10000 : -1;
            bool isNewSession = !initializedThisSession || 
                (Application.isPlaying && lastSessionId != currentSessionId && Time.frameCount < 10);
            
            if (!analysisComplete || isNewSession)
            {
                Debug.Log($"🎮 Graph Juggle: Initializing with ambient Eno/Bloom audio...");
                AnalyzeGraph(nodePositions, edgeConnections);
                InitializeAudio();
                SubscribeToPlayerEvents();
                
                activeInstance = this;
                currentLives = startingLives;
                currentScore = 0;
                streak = 0;
                gameActive = true;
                activePulses.Clear();
                successRipples.Clear();
                nextBallIndex = 0;
                brightnessMultiplier = 1f;
                currentMomentum = 1f;
                timeSinceLastCatch = 0f;
                lastBallSpawnTime = 0f;
                initializedThisSession = true;
                lastSessionId = currentSessionId;
                lastRealTime = Time.realtimeSinceStartup;
            }
            
            // Delta time
            float currentRealTime = Time.realtimeSinceStartup;
            float deltaTime = 0f;
            if (currentRealTime > lastRealTime)
            {
                deltaTime = Mathf.Min(currentRealTime - lastRealTime, 0.1f);
                lastRealTime = currentRealTime;
            }
            
            // Input
            ProcessInput(nodePositions);
            ProcessPlayerTouches();
            
            // Auto-start and multi-ball spawning
            if (autoStartPulse && gameActive && endpointNodes.Count >= 2)
            {
                float currentTime = Time.realtimeSinceStartup;
                
                // Spawn first ball if none exist
                if (activePulses.Count == 0)
                {
                    SpawnRandomPulse(time);
                    lastBallSpawnTime = currentTime;
                }
                // Spawn additional balls if under limit and interval passed
                else if (activePulses.Count < maxSimultaneousBalls && 
                         currentTime - lastBallSpawnTime >= ballSpawnInterval)
                {
                    SpawnRandomPulse(time);
                    lastBallSpawnTime = currentTime;
                }
            }
            
            // Update momentum decay
            if (enableMomentum)
            {
                timeSinceLastCatch += deltaTime;
                if (timeSinceLastCatch > 1f) // Start decay after 1 second of no catches
                {
                    currentMomentum = Mathf.Max(1f, currentMomentum - momentumDecayRate * deltaTime);
                }
            }
            
            // Update pulses
            UpdatePulses(deltaTime, time, colors, nodePositions);
            
            // Update ambient audio to pulse with light
            UpdateAmbientModulation(deltaTime);
            
            // Render endpoints
            RenderEndpoints(colors, nodePositions, time);
            
            // Apply effects
            ApplyVisualEffects(deltaTime, colors);
            
            return colors;
        }
        
        void AnalyzeGraph(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            endpointNodes.Clear();
            adjacency.Clear();
            endpointFrequencies.Clear();
            
            // Find height range
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            foreach (var pos in nodePositions)
            {
                if (pos.y < minHeight) minHeight = pos.y;
                if (pos.y > maxHeight) maxHeight = pos.y;
            }
            
            // Build adjacency
            for (int i = 0; i < nodePositions.Count; i++)
            {
                adjacency[i] = new List<int>();
            }
            
            foreach (var edge in edgeConnections)
            {
                if (edge.x < nodePositions.Count && edge.y < nodePositions.Count)
                {
                    if (!adjacency[edge.x].Contains(edge.y))
                        adjacency[edge.x].Add(edge.y);
                    if (!adjacency[edge.y].Contains(edge.x))
                        adjacency[edge.y].Add(edge.x);
                }
            }
            
            // Find endpoints
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                    endpointNodes.Add(kvp.Key);
            }
            
            // Sort by position for consistent ordering
            endpointNodes = endpointNodes.OrderBy(e => 
                nodePositions[e].x + nodePositions[e].z * 0.01f).ToList();
            
            // Assign pentatonic frequencies to each leg
            for (int i = 0; i < endpointNodes.Count; i++)
            {
                int endpoint = endpointNodes[i];
                int scaleIndex = i % PentatonicIntervals.Length;
                int octaveOffset = i / PentatonicIntervals.Length;
                int semitones = PentatonicIntervals[scaleIndex] + (octaveOffset * 12);
                
                float frequency = baseFrequency * Mathf.Pow(2f, semitones / 12f);
                endpointFrequencies[endpoint] = frequency;
                
                Debug.Log($"🎵 Leg {i}: {frequency:F1}Hz");
            }
            
            Debug.Log($"🦶 {endpointNodes.Count} legs with pentatonic tones");
            analysisComplete = true;
        }
        
        float GetNormalizedHeight(Vector3 position)
        {
            if (maxHeight <= minHeight) return 0.5f;
            return Mathf.Clamp01((position.y - minHeight) / (maxHeight - minHeight));
        }
        
        Color GetColorForHeight(float normalizedHeight)
        {
            return Color.Lerp(groundColor, skyColor, normalizedHeight);
        }
        
        void InitializeAudio()
        {
            if (audioInitialized) return;
            
            sampleRate = AudioSettings.outputSampleRate;
            
            if (audioContainer == null)
            {
                audioContainer = new GameObject("BloomAudio");
                // Don't hide - we want to see if it exists
                
                // Tone source for melodic hits
                var toneGO = new GameObject("ToneSource");
                toneGO.transform.SetParent(audioContainer.transform);
                toneSource = toneGO.AddComponent<AudioSource>();
                toneSource.playOnAwake = false;
                toneSource.volume = toneVolume;
                
                // Ambient source for background
                var ambientGO = new GameObject("AmbientSource");
                ambientGO.transform.SetParent(audioContainer.transform);
                ambientSource = ambientGO.AddComponent<AudioSource>();
                ambientSource.playOnAwake = false;
                ambientSource.loop = true;
                ambientSource.volume = ambientVolume;
                
                Debug.Log("🔊 Created audio sources");
            }
            
            // Generate tone clips for each endpoint (duration is calculated from ADSR)
            toneClips.Clear();
            foreach (var kvp in endpointFrequencies)
            {
                // Duration is determined by ADSR envelope in GenerateBellTone
                toneClips[kvp.Key] = GenerateBellTone(kvp.Value, 0f); // duration param is unused
                Debug.Log($"🔔 Generated tone for endpoint {kvp.Key}: {kvp.Value:F0}Hz");
            }
            
            // Generate ambient clip
            ambientClip = GenerateAmbientClip(10f); // 10 second loop
            ambientSource.clip = ambientClip;
            ambientSource.Play();
            Debug.Log("🌊 Ambient loop started");
            
            // Generate fail sound
            failClip = GenerateFailClip();
            
            audioInitialized = true;
            Debug.Log($"🔊 Bloom audio ready! {toneClips.Count} tones generated");
        }
        
        AudioClip GenerateBellTone(float frequency, float unusedDuration = 0f)
        {
            // Total duration calculated from ADSR envelope parameters
            float totalDuration = toneAttack + toneDecayTime + toneRelease;
            int samples = Mathf.RoundToInt(sampleRate * totalDuration);
            float[] data = new float[samples];
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                
                // ADSR Envelope
                float envelope = 0f;
                if (t < toneAttack)
                {
                    // Attack phase: ramp up
                    envelope = t / toneAttack;
                }
                else if (t < toneAttack + toneDecayTime)
                {
                    // Decay phase: drop to sustain level
                    float decayProgress = (t - toneAttack) / toneDecayTime;
                    envelope = Mathf.Lerp(1f, toneSustain, decayProgress);
                }
                else
                {
                    // Release phase: fade from sustain to zero
                    float releaseProgress = (t - toneAttack - toneDecayTime) / toneRelease;
                    envelope = toneSustain * (1f - releaseProgress);
                }
                envelope = Mathf.Max(0f, envelope);
                
                float phase = t * frequency * 2f * Mathf.PI;
                
                // Warm tone with tunable harmonics
                float sample = Mathf.Sin(phase);                      // Fundamental
                sample += harmonic2 * Mathf.Sin(phase * 2f);          // Second harmonic (octave)
                sample += harmonic3 * Mathf.Sin(phase * 3f);          // Third harmonic
                sample += 0.05f * Mathf.Sin(phase * 4f);              // Subtle 4th harmonic
                
                // Normalize based on harmonic content
                float normalizer = 1f / (1f + harmonic2 + harmonic3 + 0.05f);
                sample *= normalizer;
                
                // Apply envelope
                sample *= envelope;
                
                // Soft limiting
                sample = Mathf.Clamp(sample, -0.95f, 0.95f);
                
                data[i] = sample;
            }
            
            AudioClip clip = AudioClip.Create($"Bell_{frequency:F0}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        
        AudioClip GenerateAmbientClip(float duration)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            float[] data = new float[samples];
            
            float phase1 = 0f;
            float phase2 = 0f;
            float phase3 = 0f; // Sub-bass
            float filterState = 0f;
            
            // Use consistent random seed for smooth loop
            System.Random rand = new System.Random(42);
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                
                // Smooth filtered noise (like distant wind/breath)
                float noise = ((float)rand.NextDouble() * 2f - 1f);
                filterState = filterState * 0.997f + noise * 0.003f;
                
                // Slow undulating LFO for organic movement
                float lfo1 = Mathf.Sin(t * 0.08f) * 0.5f + 0.5f;
                float lfo2 = Mathf.Sin(t * 0.05f + 1.2f) * 0.5f + 0.5f;
                
                // Primary drone (using parameter)
                phase1 += (ambientFrequency + lfo1 * 1.5f) / sampleRate * 2f * Mathf.PI;
                float drone1 = Mathf.Sin(phase1) * 0.35f;
                
                // Second drone - perfect fifth (using parameter)
                phase2 += (ambientFrequency2 + lfo2 * 1f) / sampleRate * 2f * Mathf.PI;
                float drone2 = Mathf.Sin(phase2) * 0.25f;
                
                // Sub-bass octave below for depth
                phase3 += (ambientFrequency * 0.5f) / sampleRate * 2f * Mathf.PI;
                float subBass = Mathf.Sin(phase3) * 0.2f;
                
                // Blend: noise + drones
                float noiseComponent = filterState * ambientNoiseAmount;
                float droneComponent = (drone1 + drone2 + subBass);
                float sample = noiseComponent + droneComponent;
                
                // Normalize
                sample *= 0.5f;
                
                // Fade in/out at loop points for seamless loop
                float fadeIn = Mathf.Clamp01(t / 0.3f);
                float fadeOut = Mathf.Clamp01((duration - t) / 0.3f);
                sample *= fadeIn * fadeOut;
                
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            
            AudioClip clip = AudioClip.Create("Ambient", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        
        AudioClip GenerateFailClip()
        {
            int samples = Mathf.RoundToInt(sampleRate * 0.4f);
            float[] data = new float[samples];
            
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / 0.4f;
                
                // Gentle descending tone (not harsh)
                float envelope = Mathf.Exp(-progress * 3f);
                float freq = Mathf.Lerp(220f, 110f, progress); // Gentle descent
                
                float sample = Mathf.Sin(t * freq * 2f * Mathf.PI);
                sample += 0.2f * Mathf.Sin(t * freq * 2f * 2f * Mathf.PI); // Soft octave
                
                data[i] = sample * envelope * 0.25f;
            }
            
            AudioClip clip = AudioClip.Create("Fail", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        
        public void TriggerTone(int endpointIndex)
        {
            if (toneSource == null) return;
            
            if (toneClips.ContainsKey(endpointIndex))
            {
                // Play at gentler volume
                toneSource.PlayOneShot(toneClips[endpointIndex], toneVolume * 0.7f);
            }
        }
        
        public void TriggerSuccessTone(int endpointIndex)
        {
            if (toneSource == null) return;
            
            // Play the main tone (slightly louder for success)
            if (toneClips.ContainsKey(endpointIndex))
            {
                toneSource.PlayOneShot(toneClips[endpointIndex], toneVolume);
            }
            
            // Also play a soft fifth for harmonic richness
            if (endpointFrequencies.ContainsKey(endpointIndex))
            {
                float freq = endpointFrequencies[endpointIndex];
                AudioClip fifthClip = GenerateBellTone(freq * 1.5f, 0f); // ADSR determines duration
                toneSource.PlayOneShot(fifthClip, toneVolume * 0.4f);
            }
        }
        
        void UpdateAmbientModulation(float deltaTime)
        {
            if (ambientSource == null) return;
            
            // Calculate pulse intensity based on active pulses
            float targetIntensity = 0f;
            
            if (activePulses.Count > 0)
            {
                // Get the highest pulse height (closest to the top of the graph)
                float maxHeight = 0f;
                foreach (var pulse in activePulses)
                {
                    if (pulse.currentHeight > maxHeight)
                        maxHeight = pulse.currentHeight;
                }
                
                // Pulses at higher positions = stronger ambient swell
                targetIntensity = maxHeight;
                
                // Extra boost when catchable (dramatic moment)
                foreach (var pulse in activePulses)
                {
                    if (pulse.catchable)
                    {
                        targetIntensity = Mathf.Max(targetIntensity, 0.9f);
                        break;
                    }
                }
            }
            
            // Smooth the intensity change
            currentPulseIntensity = Mathf.Lerp(currentPulseIntensity, targetIntensity, deltaTime * 3f);
            
            // Calculate target volume: base volume + pulse modulation
            float pulseBoost = currentPulseIntensity * ambientPulseAmount * 0.6f;
            ambientTargetVolume = ambientVolume + pulseBoost;
            
            // Smoothly adjust ambient volume
            ambientSource.volume = Mathf.Lerp(ambientSource.volume, ambientTargetVolume, deltaTime * 5f);
        }
        
        void SubscribeToPlayerEvents()
        {
            if (!subscribedToPlayerEvents)
            {
                GraphPlayerController.OnEndpointTouched += OnPlayerTouchedEndpoint;
                subscribedToPlayerEvents = true;
            }
        }
        
        void OnPlayerTouchedEndpoint(int endpointIndex, Vector3 position)
        {
            if (!gameActive) return;
            if (inputMode == InputMode.None || inputMode == InputMode.MouseClick) return;
            pendingPlayerTouches.Enqueue(endpointIndex);
        }
        
        void ProcessInput(List<Vector3> nodePositions)
        {
            if (inputMode == InputMode.None || inputMode == InputMode.PlayerTouch) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (!gameActive) return;
            
            if (gameCamera == null)
            {
                gameCamera = Camera.main;
                if (gameCamera == null) return;
            }
            
            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            
            int clickedEndpoint = -1;
            float closestDistance = clickRadius;
            
            foreach (int endpoint in endpointNodes)
            {
                if (endpoint >= nodePositions.Count) continue;
                Vector3 nodePos = nodePositions[endpoint];
                
                Vector3 toPoint = nodePos - ray.origin;
                float dot = Vector3.Dot(toPoint, ray.direction);
                if (dot < 0) continue;
                
                Vector3 closestPoint = ray.origin + ray.direction * dot;
                float dist = Vector3.Distance(nodePos, closestPoint);
                
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    clickedEndpoint = endpoint;
                }
            }
            
            if (clickedEndpoint >= 0)
            {
                HandleEndpointClick(clickedEndpoint);
            }
        }
        
        void ProcessPlayerTouches()
        {
            if (inputMode == InputMode.None || inputMode == InputMode.MouseClick) return;
            while (pendingPlayerTouches.Count > 0)
            {
                int endpoint = pendingPlayerTouches.Dequeue();
                HandleEndpointClick(endpoint);
            }
        }
        
        void HandleEndpointClick(int endpointIndex)
        {
            for (int i = activePulses.Count - 1; i >= 0; i--)
            {
                var pulse = activePulses[i];
                
                if (pulse.targetEndpoint == endpointIndex && pulse.catchable && !pulse.caught)
                {
                    pulse.caught = true;
                    
                    currentScore += 10 * (streak + 1);
                    streak++;
                    
                    // Increase momentum on catch
                    if (enableMomentum)
                    {
                        currentMomentum = Mathf.Min(maxMomentumMultiplier, currentMomentum * momentumGainPerCatch);
                        timeSinceLastCatch = 0f;
                    }
                    
                    Debug.Log($"🎯 CATCH! Score: {currentScore}, Streak: {streak}, Momentum: {currentMomentum:F2}x");
                    
                    // Play the leg's tone with harmonics
                    TriggerSuccessTone(endpointIndex);
                    
                    // Spawn localized success ripple using the ball's color
                    Color rippleColor = useRainbowColors 
                        ? Color.HSVToRGB(pulse.hue, rainbowSaturation, rainbowBrightness)
                        : catchReadyColor;
                    SpawnSuccessRipple(endpointIndex, rippleColor, successFlashDuration);
                    brightnessMultiplier = successBrightnessBoost;
                    
                    // Spawn new ball and remove the caught one
                    SpawnPulseFrom(endpointIndex, Time.realtimeSinceStartup);
                    activePulses.RemoveAt(i);
                    break;
                }
            }
        }
        
        void PlayFailSound()
        {
            if (toneSource != null && failClip != null)
            {
                toneSource.PlayOneShot(failClip, toneVolume * 0.7f);
                Debug.Log("💔 Fail sound");
            }
        }
        
        void SpawnRandomPulse(float time)
        {
            if (endpointNodes.Count < 2) return;
            int startNode = endpointNodes[Random.Range(0, endpointNodes.Count)];
            SpawnPulseFrom(startNode, time);
        }
        
        void SpawnPulseFrom(int startNode, float time)
        {
            var otherEndpoints = endpointNodes.Where(e => e != startNode).ToList();
            if (otherEndpoints.Count == 0) return;
            
            int targetNode = otherEndpoints[Random.Range(0, otherEndpoints.Count)];
            List<int> path = FindPath(startNode, targetNode);
            
            if (path.Count >= 2)
            {
                // Assign fixed hue based on ball index - evenly distributed across max balls
                float hue = GetHueForBallIndex(nextBallIndex);
                var pulse = new ActivePulse(path, time, hue, nextBallIndex);
                activePulses.Add(pulse);
                nextBallIndex = (nextBallIndex + 1) % Mathf.Max(1, maxSimultaneousBalls);
                
                // Play a soft tone when pulse spawns
                TriggerTone(startNode);
            }
        }
        
        /// <summary>
        /// Get a fixed hue evenly distributed based on max simultaneous balls
        /// Colors stay consistent - ball 0 is always red, ball 1 is always the next hue, etc.
        /// </summary>
        float GetHueForBallIndex(int index)
        {
            int maxBalls = Mathf.Max(1, maxSimultaneousBalls);
            return (float)index / maxBalls;
        }
        
        List<int> FindPath(int start, int end)
        {
            if (!adjacency.ContainsKey(start) || !adjacency.ContainsKey(end))
                return new List<int>();
            
            var visited = new HashSet<int>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { start });
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                int current = path[path.Count - 1];
                
                if (current == end) return path;
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
        
        void UpdatePulses(float deltaTime, float time, Color[] colors, List<Vector3> nodePositions)
        {
            for (int i = activePulses.Count - 1; i >= 0; i--)
            {
                var pulse = activePulses[i];
                
                float pathLength = pulse.path.Count - 1;
                float effectiveSpeed = pulseSpeed * (enableMomentum ? currentMomentum : 1f);
                float progressSpeed = effectiveSpeed / (pathLength * 10f);
                pulse.progress += progressSpeed * deltaTime * speed;
                
                // Calculate current height
                int currentNodeIndex = Mathf.FloorToInt(pulse.progress * pathLength);
                currentNodeIndex = Mathf.Clamp(currentNodeIndex, 0, pulse.path.Count - 1);
                int currentNode = pulse.path[currentNodeIndex];
                
                if (currentNode < nodePositions.Count)
                {
                    pulse.currentHeight = GetNormalizedHeight(nodePositions[currentNode]);
                }
                
                // Catch window
                float distanceToEnd = 1f - pulse.progress;
                float catchThreshold = catchWindowSeconds * progressSpeed * speed;
                pulse.catchable = distanceToEnd <= catchThreshold && distanceToEnd > 0;
                
                // Missed
                if (pulse.progress >= 1f && !pulse.caught)
                {
                    currentLives--;
                    streak = 0;
                    
                    // Reduce momentum on miss
                    if (enableMomentum)
                    {
                        currentMomentum = Mathf.Max(1f, currentMomentum * (1f - momentumLossOnMiss));
                    }
                    
                    Debug.Log($"💔 Missed! Lives: {currentLives}, Momentum: {currentMomentum:F2}x");
                    
                    PlayFailSound();
                    // Spawn localized fail ripple (using ball's color tinted red)
                    Color missColor = useRainbowColors 
                        ? Color.Lerp(Color.HSVToRGB(pulse.hue, rainbowSaturation, rainbowBrightness), Color.red, 0.5f)
                        : Color.red * 0.5f;
                    SpawnSuccessRipple(pulse.targetEndpoint, missColor, 0.3f);
                    brightnessMultiplier = 0.7f;
                    
                    if (currentLives <= 0)
                    {
                        GameOver();
                    }
                    else if (activePulses.Count <= 1)
                    {
                        // Only spawn replacement if this was the last/only ball
                        SpawnRandomPulse(time);
                    }
                    
                    activePulses.RemoveAt(i);
                    continue;
                }
                
                // Render
                RenderPulse(pulse, colors, nodePositions);
            }
        }
        
        void RenderPulse(ActivePulse pulse, Color[] colors, List<Vector3> nodePositions)
        {
            int pathLength = pulse.path.Count;
            float pulsePosition = pulse.progress * (pathLength - 1);
            
            // Get the ball's unique color
            Color ballColor;
            if (useRainbowColors)
            {
                ballColor = Color.HSVToRGB(pulse.hue, rainbowSaturation, rainbowBrightness);
            }
            else
            {
                float height = GetNormalizedHeight(nodePositions[Mathf.Clamp((int)pulsePosition, 0, pathLength - 1)]);
                ballColor = GetColorForHeight(height);
            }
            
            // Render based on selected style
            if (pulseStyle == PulseStyle.VelocityTrail)
            {
                // VELOCITY TRAIL MODE
                float effectiveTrailLength = trailLength * 0.3f; // Scale for node mode
                if (trailStretchWithMomentum && enableMomentum)
                {
                    effectiveTrailLength *= currentMomentum;
                }
                
                // Render trail behind the ball
                for (int i = 0; i < pathLength; i++)
                {
                    float signedDist = i - pulsePosition;
                    
                    if (signedDist < 0 && signedDist > -effectiveTrailLength)
                    {
                        int nodeIndex = pulse.path[i];
                        if (nodeIndex >= colors.Length || nodeIndex >= nodePositions.Count) continue;
                        
                        float trailPos = -signedDist / effectiveTrailLength;
                        float trailFade = (1f - trailPos) * trailIntensity;
                        
                        Color trailColor;
                        if (useRainbowColors)
                        {
                            float fadedSat = Mathf.Lerp(rainbowSaturation, rainbowSaturation * 0.5f, trailPos);
                            float fadedBright = Mathf.Lerp(rainbowBrightness, rainbowBrightness * 0.3f, trailPos);
                            trailColor = Color.HSVToRGB(pulse.hue, fadedSat, fadedBright);
                        }
                        else
                        {
                            trailColor = ballColor * (1f - trailPos * 0.7f);
                        }
                        
                        colors[nodeIndex] = Color.Lerp(colors[nodeIndex], trailColor, trailFade);
                    }
                }
                
                // Compact bright head
                float headWidth = pulseWidth * 0.5f;
                for (int i = 0; i < pathLength; i++)
                {
                    float distance = Mathf.Abs(i - pulsePosition);
                    
                    if (distance <= headWidth)
                    {
                        float intensity = pulseShape.Evaluate(1f - (distance / headWidth));
                        int nodeIndex = pulse.path[i];
                        
                        if (nodeIndex < colors.Length && nodeIndex < nodePositions.Count)
                        {
                            Color renderColor = ballColor;
                            if (pulse.catchable)
                            {
                                renderColor = Color.Lerp(renderColor, catchReadyColor, 0.5f);
                                intensity *= catchBrightnessBoost;
                            }
                            colors[nodeIndex] = Color.Lerp(colors[nodeIndex], renderColor, intensity);
                        }
                    }
                }
            }
            else
            {
                // SYMMETRIC GLOW MODE
                for (int i = 0; i < pathLength; i++)
                {
                    float distance = Mathf.Abs(i - pulsePosition);
                    
                    if (distance <= pulseWidth)
                    {
                        float intensity = pulseShape.Evaluate(1f - (distance / pulseWidth));
                        int nodeIndex = pulse.path[i];
                        
                        if (nodeIndex < colors.Length && nodeIndex < nodePositions.Count)
                        {
                            Color renderColor = ballColor;
                            if (pulse.catchable)
                            {
                                renderColor = Color.Lerp(renderColor, catchReadyColor, 0.5f);
                                intensity *= catchBrightnessBoost;
                            }
                            colors[nodeIndex] = Color.Lerp(colors[nodeIndex], renderColor, intensity);
                        }
                    }
                }
            }
            
            // Blink target when catchable
            if (pulse.catchable && pulse.targetEndpoint < colors.Length)
            {
                float blink = Mathf.Sin(Time.realtimeSinceStartup * 12f) * 0.5f + 0.5f;
                colors[pulse.targetEndpoint] = Color.Lerp(catchReadyColor, Color.white, blink);
            }
            
            // Store progress for velocity calculation
            pulse.lastProgress = pulse.progress;
        }
        
        void RenderEndpoints(Color[] colors, List<Vector3> nodePositions, float time)
        {
            float idlePulse = Mathf.Sin(time * 0.5f) * 0.5f + 0.5f;
            
            foreach (int endpoint in endpointNodes)
            {
                if (endpoint >= colors.Length || endpoint >= nodePositions.Count) continue;
                if (colors[endpoint].maxColorComponent > 0.5f) continue;
                
                float height = GetNormalizedHeight(nodePositions[endpoint]);
                Color endpointColor = GetColorForHeight(height);
                float intensity = 0.15f + idlePulse * 0.1f;
                
                colors[endpoint] = Color.Lerp(inactiveColor, endpointColor, intensity);
            }
        }
        
        void ApplyVisualEffects(float deltaTime, Color[] colors)
        {
            float currentTime = Time.realtimeSinceStartup;
            
            // Render success ripples emanating from endpoints
            for (int r = successRipples.Count - 1; r >= 0; r--)
            {
                var ripple = successRipples[r];
                float elapsed = currentTime - ripple.startTime;
                
                if (elapsed >= ripple.duration)
                {
                    successRipples.RemoveAt(r);
                    continue;
                }
                
                float normalizedTime = elapsed / ripple.duration;
                float rippleProgress = elapsed * ripple.speed; // How far the ripple has traveled
                float fadeOut = 1f - normalizedTime; // Fade as it ages
                
                // Render this ripple on the graph
                RenderRippleOnGraph(colors, ripple.endpointIndex, rippleProgress, ripple.color, fadeOut);
            }
            
            brightnessMultiplier = Mathf.Lerp(brightnessMultiplier, 1f, deltaTime * 3f);
            
            if (Mathf.Abs(brightnessMultiplier - 1f) > 0.01f)
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] *= brightnessMultiplier;
                }
            }
        }
        
        void RenderRippleOnGraph(Color[] colors, int startEndpoint, float progress, Color rippleColor, float intensity)
        {
            if (cachedPositions == null || startEndpoint >= cachedPositions.Count) return;
            
            Vector3 startPos = cachedPositions[startEndpoint];
            float rippleWidth = pulseWidth * 2f; // Make ripple a bit wider than pulses
            
            for (int i = 0; i < colors.Length && i < cachedPositions.Count; i++)
            {
                float dist = Vector3.Distance(cachedPositions[i], startPos);
                
                // Check if this node is within the ripple band
                float distFromRippleFront = Mathf.Abs(dist - progress);
                
                if (distFromRippleFront < rippleWidth)
                {
                    // Smooth falloff from center of ripple
                    float localIntensity = 1f - (distFromRippleFront / rippleWidth);
                    localIntensity = Mathf.Pow(localIntensity, 0.5f); // Softer falloff
                    
                    float finalIntensity = localIntensity * intensity * 0.6f;
                    colors[i] = Color.Lerp(colors[i], rippleColor, finalIntensity);
                }
            }
        }
        
        void SpawnSuccessRipple(int endpointIndex, Color color, float duration)
        {
            successRipples.Add(new SuccessRipple
            {
                endpointIndex = endpointIndex,
                startTime = Time.realtimeSinceStartup,
                duration = duration,
                color = color,
                speed = pulseSpeed * 1.5f // Ripples move a bit faster than balls
            });
        }
        
        void GameOver()
        {
            gameActive = false;
            Debug.Log($"🎮 GAME OVER! Final Score: {currentScore}");
            // Spawn fail ripples from all endpoints for dramatic effect
            foreach (int endpoint in endpointNodes)
            {
                SpawnSuccessRipple(endpoint, Color.red * 0.3f, 1f);
            }
        }
        
        [ContextMenu("Restart Game")]
        public void RestartGame()
        {
            currentScore = 0;
            currentLives = startingLives;
            streak = 0;
            gameActive = true;
            activePulses.Clear();
            successRipples.Clear();
            nextBallIndex = 0;
            brightnessMultiplier = 1f;
            currentMomentum = 1f;
            timeSinceLastCatch = 0f;
            lastBallSpawnTime = 0f;
        }
        
        // Public accessors for simulator
        public List<int> GetEndpointNodes() => endpointNodes;
        public int ActivePulseCount => activePulses.Count;
        public bool IsGameActive => gameActive;
        public float CurrentMomentum => currentMomentum;
        public int CurrentScore => currentScore;
        public int CurrentStreak => streak;
        
        public bool TryGetCatchableEndpoint(out int endpoint)
        {
            endpoint = -1;
            foreach (var pulse in activePulses)
            {
                if (pulse.catchable && !pulse.caught)
                {
                    endpoint = pulse.targetEndpoint;
                    return true;
                }
            }
            return false;
        }
        
        public List<int> GetAllCatchableEndpoints()
        {
            var catchable = new List<int>();
            foreach (var pulse in activePulses)
            {
                if (pulse.catchable && !pulse.caught)
                {
                    catchable.Add(pulse.targetEndpoint);
                }
            }
            return catchable;
        }
        
        // For simulator to force spawn balls
        public void ForceSpawnBall()
        {
            if (gameActive && endpointNodes.Count >= 2)
            {
                SpawnRandomPulse(Time.realtimeSinceStartup);
            }
        }
        
        public void SetMaxBalls(int count)
        {
            maxSimultaneousBalls = Mathf.Clamp(count, 1, 10);
        }
        
        void OnEnable()
        {
            analysisComplete = false;
            initializedThisSession = false;
            audioInitialized = false;
            ledMappingInitialized = false; // Reset LED mapping
        }
        
        /// <summary>
        /// Clean up audio and state when animation is disabled or changed
        /// </summary>
        public void CleanupAudio()
        {
            if (toneSource != null)
            {
                toneSource.Stop();
            }
            if (ambientSource != null)
            {
                ambientSource.Stop();
            }
            
            if (audioContainer != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(audioContainer);
                else
                    Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }
        }
        
        void OnDisable()
        {
            if (subscribedToPlayerEvents)
            {
                GraphPlayerController.OnEndpointTouched -= OnPlayerTouchedEndpoint;
                subscribedToPlayerEvents = false;
            }
            
            CleanupAudio();
        }
        
        void OnValidate()
        {
            pulseSpeed = Mathf.Max(0.1f, pulseSpeed);
            catchWindowSeconds = Mathf.Clamp(catchWindowSeconds, 0.1f, 2f);
            baseFrequency = Mathf.Clamp(baseFrequency, 50f, 500f);
            // ADSR parameters are already clamped via Range attributes
        }
    }
}
