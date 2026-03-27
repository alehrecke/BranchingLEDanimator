using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Player;

namespace BranchingLEDAnimator.Animation
{
    /// <summary>
    /// Interactive musical animation where each leg/endpoint plays a tone when touched
    /// Tones are assigned from a musical scale to create pleasing harmonies
    /// </summary>
    [CreateAssetMenu(fileName = "ChordTouchAnimation", menuName = "LED Animations/Interactive/Chord Touch")]
    public class ChordTouchAnimation : LEDAnimationType
    {
        public enum AudioMode
        {
            ProceduralTones,    // Generate sine wave tones mathematically
            CustomClips         // Use assigned AudioClip files (like bird calls)
        }
        
        [Header("Audio Mode")]
        [Tooltip("Choose between procedural tones or custom audio clips")]
        public AudioMode audioMode = AudioMode.ProceduralTones;
        
        [Header("Custom Audio Clips (when AudioMode = CustomClips)")]
        [Tooltip("Audio clips to play for each endpoint. Clips are assigned to endpoints in order by height (lowest to highest).")]
        public AudioClip[] customClips;
        
        [Tooltip("Should custom clips loop while held?")]
        public bool loopCustomClips = true;
        
        [Header("Musical Settings (when AudioMode = ProceduralTones)")]
        public MusicalScale scale = MusicalScale.MajorPentatonic;
        public float baseFrequency = 261.63f; // C4 (middle C)
        public int baseOctave = 4;
        
        [Header("Volume & Envelope")]
        [Tooltip("Volume of the sustained tone")]
        [Range(0f, 1f)]
        public float toneVolume = 0.5f;
        [Tooltip("Attack time for tone fade-in (seconds)")]
        public float toneAttack = 0.1f;
        [Tooltip("Release time for tone fade-out after releasing (seconds)")]
        public float toneRelease = 0.8f;
        
        [Header("Touch Glow (Sustained)")]
        [Tooltip("Color of the glow at the touched endpoint")]
        public Color touchGlowColor = new Color(1f, 0.9f, 0.5f);
        [Tooltip("How many nodes around the touch point glow brightly")]
        [Range(1, 100)]
        public int touchGlowRadius = 20;
        [Tooltip("Intensity of the sustained touch glow")]
        [Range(0.5f, 3f)]
        public float touchGlowIntensity = 2f;
        [Tooltip("Subtle pulse speed of touch glow while held")]
        public float touchGlowPulseSpeed = 2f;
        [Tooltip("Amount of pulsing in the touch glow")]
        [Range(0f, 0.5f)]
        public float touchGlowPulseAmount = 0.15f;
        
        [Header("Outward Pulse (While Held)")]
        [Tooltip("Color of pulses radiating outward from touch")]
        public Color pulseColor = new Color(1f, 0.8f, 0.2f);
        [Tooltip("Speed of outward pulse propagation")]
        public float pulseSpeed = 10f;
        [Tooltip("How often a new pulse spawns while holding (seconds). 0 = no repeated pulses")]
        public float pulseInterval = 0.5f;
        [Tooltip("How long each pulse lasts")]
        public float pulseFadeTime = 2f;
        
        [Header("Pulse Shape")]
        [Tooltip("Width of the pulse wave in nodes")]
        [Range(1f, 50f)]
        public float pulseWidth = 10f;
        [Tooltip("Maximum distance (in nodes) the pulse will travel. 0 = unlimited (all LEDs)")]
        [Range(0f, 500f)]
        public float pulseRange = 0f;
        [Tooltip("How quickly pulse intensity falls off with distance. Higher = faster falloff")]
        [Range(0.1f, 3f)]
        public float pulseFalloff = 1f;
        [Tooltip("Intensity falloff curve over distance (0=center of pulse, 1=edge)")]
        public AnimationCurve pulseShape = AnimationCurve.EaseInOut(0, 1, 1, 0);
        [Tooltip("Additional intensity decay over the pulse's total travel distance")]
        public AnimationCurve distanceDecay = AnimationCurve.Linear(0, 1, 1, 0.2f);
        
        [Header("Idle Glow")]
        public Color endpointIdleColor = new Color(0.1f, 0.2f, 0.4f);
        public float idlePulseSpeed = 1f;
        public float idlePulseAmount = 0.3f;
        
        [Header("Debug")]
        public bool showNoteAssignments = true;
        public bool verboseDebug = true;
        
        public enum MusicalScale
        {
            MajorPentatonic,    // C D E G A - very pleasant, no dissonance
            MinorPentatonic,    // C Eb F G Bb - bluesy, pleasant
            Major,              // C D E F G A B
            Minor,              // C D Eb F G Ab Bb
            Lydian,             // C D E F# G A B - dreamy
            WholeTone           // C D E F# G# A# - ethereal
        }
        
        // Scale intervals (semitones from root)
        private static readonly Dictionary<MusicalScale, int[]> ScaleIntervals = new Dictionary<MusicalScale, int[]>
        {
            { MusicalScale.MajorPentatonic, new[] { 0, 2, 4, 7, 9 } },
            { MusicalScale.MinorPentatonic, new[] { 0, 3, 5, 7, 10 } },
            { MusicalScale.Major, new[] { 0, 2, 4, 5, 7, 9, 11 } },
            { MusicalScale.Minor, new[] { 0, 2, 3, 5, 7, 8, 10 } },
            { MusicalScale.Lydian, new[] { 0, 2, 4, 6, 7, 9, 11 } },
            { MusicalScale.WholeTone, new[] { 0, 2, 4, 6, 8, 10 } }
        };
        
        // Active pulses (outward ripples)
        private class TouchPulse
        {
            public int sourceEndpoint;
            public List<int> pathNodes;
            public float startTime;
            public float progress;
            public float startOffset; // Distance offset from endpoint (e.g., edge of glow)
            public Color color;
        }
        
        // Held endpoint state
        private class HeldEndpoint
        {
            public int nodeIndex;
            public float pressTime;
            public float lastPulseTime;
            public float currentVolume;
            public float targetVolume;
            public List<int> pathNodes;
        }
        
        // State
        private List<int> endpointNodes = new List<int>();
        private Dictionary<int, float> endpointFrequencies = new Dictionary<int, float>();
        private Dictionary<int, Color> endpointColors = new Dictionary<int, Color>();
        private Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        private List<TouchPulse> activePulses = new List<TouchPulse>();
        private Dictionary<int, HeldEndpoint> heldEndpoints = new Dictionary<int, HeldEndpoint>();
        private bool analysisComplete = false;
        private bool subscribedToEvents = false;
        private float lastRealTime = 0f;
        private List<Vector3> cachedNodePositions;
        
        // Audio - now using looping sources for sustained tones
        private Dictionary<int, AudioSource> audioSources = new Dictionary<int, AudioSource>();
        private GameObject audioContainer;
        
        protected override Color[] CalculateBaseNodeColors(
            List<Vector3> nodePositions,
            List<Vector2Int> edgeConnections,
            List<int> sourceNodes,
            float time,
            int frame)
        {
            Color[] colors = new Color[nodePositions.Count];
            
            // Initialize with inactive
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = inactiveColor;
            }
            
            // Analyze graph and assign notes
            if (!analysisComplete)
            {
                AnalyzeGraphAndAssignNotes(nodePositions, edgeConnections);
                SetupAudio();
                SubscribeToPlayerEvents();
            }
            
            // Calculate delta time - ensure we always have a valid delta
            float currentRealTime = Time.realtimeSinceStartup;
            float deltaTime = Mathf.Clamp(currentRealTime - lastRealTime, 0.001f, 0.1f);
            lastRealTime = currentRealTime;
            
            // Update held endpoints (audio fade, spawn pulses)
            UpdateHeldEndpoints(currentRealTime, deltaTime);
            
            // Update and render outward pulses
            UpdatePulses(deltaTime, colors);
            
            // Render sustained glow for held endpoints (drawn on top)
            RenderHeldEndpointGlow(time, colors);
            
            // Render idle endpoint glow (for non-held endpoints)
            RenderEndpointGlow(time, colors);
            
            return colors;
        }
        
        void AnalyzeGraphAndAssignNotes(List<Vector3> nodePositions, List<Vector2Int> edgeConnections)
        {
            // Cache positions for later use in SetupAudio
            cachedNodePositions = nodePositions;
            
            endpointNodes.Clear();
            adjacency.Clear();
            endpointFrequencies.Clear();
            endpointColors.Clear();
            
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
            
            // Find endpoints (valence 1)
            foreach (var kvp in adjacency)
            {
                if (kvp.Value.Count == 1)
                {
                    endpointNodes.Add(kvp.Key);
                }
            }
            
            // Sort endpoints by position for consistent note assignment
            endpointNodes = endpointNodes.OrderBy(e => nodePositions[e].x + nodePositions[e].z * 0.1f).ToList();
            
            // Assign notes from scale
            int[] intervals = ScaleIntervals[scale];
            
            for (int i = 0; i < endpointNodes.Count; i++)
            {
                int endpoint = endpointNodes[i];
                
                // Calculate note: cycle through scale, ascending octaves
                int scaleIndex = i % intervals.Length;
                int octaveOffset = i / intervals.Length;
                int semitones = intervals[scaleIndex] + (octaveOffset * 12);
                
                // Frequency = base * 2^(semitones/12)
                float frequency = baseFrequency * Mathf.Pow(2f, semitones / 12f);
                endpointFrequencies[endpoint] = frequency;
                
                // Assign color based on note (hue from scale position)
                float hue = (float)scaleIndex / intervals.Length;
                endpointColors[endpoint] = Color.HSVToRGB(hue, 0.8f, 1f);
                
                if (showNoteAssignments)
                {
                    string noteName = GetNoteName(semitones);
                    Debug.Log($"🎵 Endpoint {endpoint}: {noteName} ({frequency:F1} Hz)");
                }
            }
            
            Debug.Log($"🎹 Chord Touch: Assigned {endpointNodes.Count} endpoints to {scale} scale");
            analysisComplete = true;
        }
        
        string GetNoteName(int semitones)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = semitones % 12;
            int octave = baseOctave + (semitones / 12);
            return noteNames[noteIndex] + octave;
        }
        
        void SetupAudio()
        {
            // Create audio container if needed
            if (audioContainer == null)
            {
                audioContainer = new GameObject("ChordTouchAudio");
                audioContainer.hideFlags = HideFlags.HideAndDontSave;
            }
            
            // Sort endpoints by height for clip assignment
            var sortedEndpoints = endpointNodes.OrderBy(e => cachedNodePositions[e].y).ToList();
            
            // Create audio source for each endpoint
            for (int i = 0; i < sortedEndpoints.Count; i++)
            {
                int endpoint = sortedEndpoints[i];
                
                if (!audioSources.ContainsKey(endpoint))
                {
                    var go = new GameObject($"Tone_{endpoint}");
                    go.transform.SetParent(audioContainer.transform);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.volume = 0f; // Start silent, we control volume via attack/release
                    
                    // Assign clip based on audio mode
                    if (audioMode == AudioMode.CustomClips && customClips != null && customClips.Length > 0)
                    {
                        // Use custom clips - assign by height order, cycling if fewer clips than endpoints
                        int clipIndex = i % customClips.Length;
                        source.clip = customClips[clipIndex];
                        source.loop = loopCustomClips;
                        
                        if (showNoteAssignments && customClips[clipIndex] != null)
                        {
                            Debug.Log($"🐦 Endpoint {endpoint} (height {i+1}/{sortedEndpoints.Count}): {customClips[clipIndex].name}");
                        }
                    }
                    else
                    {
                        // Use procedural tones
                        source.loop = true;
                        float frequency = endpointFrequencies[endpoint];
                        source.clip = GenerateLoopingToneClip(frequency);
                    }
                    
                    audioSources[endpoint] = source;
                }
            }
            
            string modeStr = audioMode == AudioMode.CustomClips ? "custom clips" : "procedural tones";
            Debug.Log($"🔊 Created {audioSources.Count} audio sources using {modeStr}");
            
            // Check if AudioListener exists
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                Debug.LogError("❌ NO AUDIO LISTENER IN SCENE! Audio won't play!");
            }
            else
            {
                Debug.Log($"✅ AudioListener found on: {listener.gameObject.name}");
            }
        }
        
        AudioClip GenerateLoopingToneClip(float frequency)
        {
            int sampleRate = 44100;
            
            // Calculate loop length that is an exact multiple of wavelength for seamless looping
            float wavelengthSamples = sampleRate / frequency;
            int numCycles = Mathf.Max(10, Mathf.RoundToInt(frequency / 10f)); // More cycles for smoother loop
            int sampleCount = Mathf.RoundToInt(wavelengthSamples * numCycles);
            
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                
                // Main tone (sine wave)
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t);
                
                // Add harmonics for richer, warmer sound
                sample += 0.25f * Mathf.Sin(2f * Mathf.PI * frequency * 2f * t); // Octave
                sample += 0.12f * Mathf.Sin(2f * Mathf.PI * frequency * 3f * t); // Fifth above octave
                sample += 0.08f * Mathf.Sin(2f * Mathf.PI * frequency * 4f * t); // Two octaves
                sample += 0.04f * Mathf.Sin(2f * Mathf.PI * frequency * 5f * t); // Major third above 2 octaves
                
                sample *= 0.35f; // Normalize
                
                samples[i] = sample;
            }
            
            AudioClip clip = AudioClip.Create($"SustainedTone_{frequency:F0}Hz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        
        void SubscribeToPlayerEvents()
        {
            // Always unsubscribe first to avoid duplicate handlers after recompiles
            GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
            GraphPlayerController.OnEndpointReleased -= OnEndpointReleased;
            
            // Now subscribe fresh
            GraphPlayerController.OnEndpointPressed += OnEndpointPressed;
            GraphPlayerController.OnEndpointReleased += OnEndpointReleased;
            subscribedToEvents = true;
            
            // Reset timing to avoid big deltaTime on first frame
            lastRealTime = Time.realtimeSinceStartup;
            
            Debug.Log("🎵 Subscribed to player press/release events");
        }
        
        void OnEndpointPressed(int endpointIndex, Vector3 position)
        {
            if (!endpointNodes.Contains(endpointIndex)) return;
            if (heldEndpoints.ContainsKey(endpointIndex)) return; // Already held
            
            float currentTime = Time.realtimeSinceStartup;
            
            // Create held endpoint state
            var pathNodes = GetConnectedPath(endpointIndex);
            var held = new HeldEndpoint
            {
                nodeIndex = endpointIndex,
                pressTime = currentTime,
                lastPulseTime = currentTime,
                currentVolume = 0f,
                targetVolume = toneVolume,
                pathNodes = pathNodes
            };
            heldEndpoints[endpointIndex] = held;
            
            if (verboseDebug)
            {
                Debug.Log($"✅ Added held endpoint {endpointIndex}: pathNodes={pathNodes.Count}, heldEndpoints.Count={heldEndpoints.Count}");
            }
            
            // Start playing sustained tone with fade-in
            if (audioSources.ContainsKey(endpointIndex))
            {
                var source = audioSources[endpointIndex];
                source.volume = 0f; // Start silent
                held.currentVolume = 0f; // Will fade in via UpdateHeldEndpoints
                source.Play();
                float freq = endpointFrequencies[endpointIndex];
                Debug.Log($"🔊 PRESS: {GetNoteName(GetSemitones(freq))} ({freq:F1} Hz) - fading in over {toneAttack}s");
            }
            else
            {
                Debug.LogWarning($"⚠️ No audio source for endpoint {endpointIndex}! audioSources.Count={audioSources.Count}");
            }
            
            // Spawn initial outward pulse
            SpawnPulseFromEndpoint(endpointIndex, currentTime);
        }
        
        void OnEndpointReleased(int endpointIndex, Vector3 position)
        {
            if (!heldEndpoints.ContainsKey(endpointIndex)) return;
            
            // Mark for fade out (don't remove yet - UpdateHeldEndpoints will handle that)
            heldEndpoints[endpointIndex].targetVolume = 0f;
            
            float freq = endpointFrequencies.ContainsKey(endpointIndex) ? endpointFrequencies[endpointIndex] : 440f;
            Debug.Log($"🎵 RELEASE: {GetNoteName(GetSemitones(freq))} - fading out over {toneRelease}s");
        }
        
        void UpdateHeldEndpoints(float currentTime, float deltaTime)
        {
            List<int> toRemove = new List<int>();
            
            foreach (var kvp in heldEndpoints)
            {
                var held = kvp.Value;
                int endpoint = kvp.Key;
                
                // Smooth volume changes (attack/release)
                float volumeSpeed = held.targetVolume > held.currentVolume 
                    ? (1f / Mathf.Max(0.01f, toneAttack)) 
                    : (1f / Mathf.Max(0.01f, toneRelease));
                    
                held.currentVolume = Mathf.MoveTowards(held.currentVolume, held.targetVolume, volumeSpeed * deltaTime);
                
                // Update audio source volume
                if (audioSources.ContainsKey(endpoint))
                {
                    audioSources[endpoint].volume = held.currentVolume;
                }
                
                // If fully faded out and target is 0, remove
                if (held.targetVolume <= 0f && held.currentVolume <= 0.001f)
                {
                    if (audioSources.ContainsKey(endpoint))
                    {
                        audioSources[endpoint].Stop();
                    }
                    toRemove.Add(endpoint);
                    continue;
                }
                
                // Spawn periodic pulses while held (if interval > 0)
                if (held.targetVolume > 0f && pulseInterval > 0f)
                {
                    if (currentTime - held.lastPulseTime >= pulseInterval)
                    {
                        SpawnPulseFromEndpoint(endpoint, currentTime);
                        held.lastPulseTime = currentTime;
                    }
                }
            }
            
            // Remove released endpoints that have faded out
            foreach (int endpoint in toRemove)
            {
                heldEndpoints.Remove(endpoint);
            }
        }
        
        void SpawnPulseFromEndpoint(int endpoint, float currentTime)
        {
            var pulse = new TouchPulse
            {
                sourceEndpoint = endpoint,
                pathNodes = GetConnectedPath(endpoint),
                startTime = currentTime,
                progress = 0f,
                startOffset = touchGlowRadius, // Begin at edge of glow
                color = endpointColors.ContainsKey(endpoint) ? endpointColors[endpoint] : pulseColor
            };
            activePulses.Add(pulse);
        }
        
        private float lastGlowDebugTime = 0f;
        
        void RenderHeldEndpointGlow(float time, Color[] colors)
        {
            // Debug: log held state periodically
            if (verboseDebug && heldEndpoints.Count > 0 && time - lastGlowDebugTime > 1f)
            {
                lastGlowDebugTime = time;
                Debug.Log($"💡 GLOW: {heldEndpoints.Count} held endpoints, touchGlowRadius={touchGlowRadius}");
            }
            
            foreach (var kvp in heldEndpoints)
            {
                var held = kvp.Value;
                if (held.pathNodes == null || held.pathNodes.Count == 0) 
                {
                    if (verboseDebug) Debug.LogWarning($"⚠️ Held endpoint {kvp.Key} has no path nodes!");
                    continue;
                }
                
                // Subtle pulse while held
                float pulse = 1f + Mathf.Sin(time * touchGlowPulseSpeed) * touchGlowPulseAmount;
                
                // Use currentVolume for smooth fade in/out of the glow
                float volumeNormalized = held.currentVolume / Mathf.Max(0.001f, toneVolume);
                float intensity = touchGlowIntensity * pulse * volumeNormalized;
                
                // Get color for this endpoint based on its musical note
                Color glowColor = endpointColors.ContainsKey(held.nodeIndex) 
                    ? endpointColors[held.nodeIndex] 
                    : touchGlowColor;
                
                // Debug first time
                if (verboseDebug && time - lastGlowDebugTime < 0.1f)
                {
                    Debug.Log($"💡 Rendering glow for endpoint {held.nodeIndex}: pathNodes={held.pathNodes.Count}, intensity={intensity:F2}, color={glowColor}");
                }
                
                // Glow the touch point and nearby nodes
                int nodesToLight = Mathf.Min(touchGlowRadius, held.pathNodes.Count);
                for (int i = 0; i < nodesToLight; i++)
                {
                    int nodeIndex = held.pathNodes[i];
                    if (nodeIndex >= colors.Length) continue;
                    
                    // Falloff from center
                    float distFalloff = 1f - (i / (float)touchGlowRadius);
                    distFalloff = distFalloff * distFalloff; // Quadratic falloff
                    
                    // Blend glow with existing color (take max to ensure visibility)
                    float finalIntensity = Mathf.Clamp01(intensity * distFalloff);
                    Color targetColor = glowColor * finalIntensity;
                    colors[nodeIndex] = Color.Lerp(colors[nodeIndex], targetColor, Mathf.Max(0.8f, finalIntensity));
                }
            }
        }
        
        [ContextMenu("Debug: Show Held Endpoints")]
        void DebugShowHeldEndpoints()
        {
            Debug.Log($"🔍 Currently held endpoints: {heldEndpoints.Count}");
            foreach (var kvp in heldEndpoints)
            {
                var held = kvp.Value;
                Debug.Log($"  - Endpoint {kvp.Key}: targetVol={held.targetVolume:F2}, currentVol={held.currentVolume:F2}, pathNodes={held.pathNodes?.Count ?? 0}");
            }
            Debug.Log($"🔍 Active pulses: {activePulses.Count}");
            Debug.Log($"🔍 Audio sources: {audioSources.Count}");
            Debug.Log($"🔍 Analysis complete: {analysisComplete}");
            Debug.Log($"🔍 Subscribed to events: {subscribedToEvents}");
        }
        
        [ContextMenu("Debug: Simulate Press Endpoint 0")]
        void DebugSimulatePress()
        {
            if (endpointNodes.Count > 0)
            {
                int endpoint = endpointNodes[0];
                Debug.Log($"🧪 Simulating PRESS on endpoint {endpoint}");
                OnEndpointPressed(endpoint, Vector3.zero);
            }
            else
            {
                Debug.LogWarning("No endpoints found - run animation first to analyze graph");
            }
        }
        
        [ContextMenu("Debug: Simulate Release Endpoint 0")]
        void DebugSimulateRelease()
        {
            if (endpointNodes.Count > 0)
            {
                int endpoint = endpointNodes[0];
                Debug.Log($"🧪 Simulating RELEASE on endpoint {endpoint}");
                OnEndpointReleased(endpoint, Vector3.zero);
            }
        }
        
        [ContextMenu("Debug: Play Test Tone (440Hz)")]
        void DebugPlayTestTone()
        {
            // Create a simple test tone to verify audio is working
            var testGO = new GameObject("TestTone");
            testGO.hideFlags = HideFlags.HideAndDontSave;
            var source = testGO.AddComponent<AudioSource>();
            
            // Generate a simple 440Hz tone
            int sampleRate = 44100;
            int sampleCount = sampleRate; // 1 second
            float[] samples = new float[sampleCount];
            float freq = 440f;
            
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f;
            }
            
            var clip = AudioClip.Create("TestTone440Hz", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            
            source.clip = clip;
            source.volume = 1f;
            source.Play();
            
            Debug.Log($"🔊 TEST: Playing 440Hz tone for 1 second. volume=1, clip={clip != null}, isPlaying={source.isPlaying}");
            
            // Destroy after playing
            Object.Destroy(testGO, 1.5f);
        }
        
        [ContextMenu("Debug: Force Full Volume on Endpoint 0")]
        void DebugForceFullVolume()
        {
            if (audioSources.Count > 0 && endpointNodes.Count > 0)
            {
                int endpoint = endpointNodes[0];
                if (audioSources.ContainsKey(endpoint))
                {
                    var source = audioSources[endpoint];
                    source.volume = 1f;
                    source.Play();
                    Debug.Log($"🔊 Forced full volume on endpoint {endpoint}: isPlaying={source.isPlaying}, clip={source.clip != null}");
                }
            }
            else
            {
                Debug.LogWarning("No audio sources or endpoints available");
            }
        }
        
        int GetSemitones(float frequency)
        {
            return Mathf.RoundToInt(12f * Mathf.Log(frequency / baseFrequency, 2f));
        }
        
        List<int> GetConnectedPath(int startNode)
        {
            // BFS to get nodes in order of distance from start
            var path = new List<int>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            
            queue.Enqueue(startNode);
            visited.Add(startNode);
            
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                path.Add(current);
                
                if (adjacency.ContainsKey(current))
                {
                    foreach (int neighbor in adjacency[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            return path;
        }
        
        void UpdatePulses(float deltaTime, Color[] colors)
        {
            for (int i = activePulses.Count - 1; i >= 0; i--)
            {
                var pulse = activePulses[i];
                
                float elapsed = Time.realtimeSinceStartup - pulse.startTime;
                pulse.progress = pulse.startOffset + (elapsed * pulseSpeed);
                
                // Remove expired pulses
                if (elapsed > pulseFadeTime)
                {
                    activePulses.RemoveAt(i);
                    continue;
                }
                
                // Calculate effective range (0 = unlimited = full path)
                float effectiveRange = pulseRange > 0 ? pulseRange : pulse.pathNodes.Count;
                
                // Render pulse
                float fadeMultiplier = 1f - (elapsed / pulseFadeTime);
                
                for (int j = 0; j < pulse.pathNodes.Count; j++)
                {
                    int nodeIndex = pulse.pathNodes[j];
                    if (nodeIndex >= colors.Length) continue;
                    
                    float nodeDistance = j;
                    
                    // Skip nodes beyond the pulse range
                    if (pulseRange > 0 && nodeDistance > pulseRange) continue;
                    
                    float distFromPulse = Mathf.Abs(nodeDistance - pulse.progress);
                    
                    if (distFromPulse < pulseWidth)
                    {
                        // Base intensity from pulse shape curve
                        float pulseIntensity = pulseShape.Evaluate(1f - (distFromPulse / pulseWidth));
                        
                        // Apply falloff exponent for sharper/softer edges
                        pulseIntensity = Mathf.Pow(pulseIntensity, pulseFalloff);
                        
                        // Apply distance decay based on how far the pulse has traveled
                        float travelNormalized = Mathf.Clamp01(pulse.progress / effectiveRange);
                        float decayMultiplier = distanceDecay.Evaluate(travelNormalized);
                        
                        // Combine all intensity factors
                        float intensity = pulseIntensity * fadeMultiplier * decayMultiplier;
                        
                        Color nodeColor = Color.Lerp(colors[nodeIndex], pulse.color, intensity);
                        colors[nodeIndex] = nodeColor;
                    }
                }
            }
        }
        
        void RenderEndpointGlow(float time, Color[] colors)
        {
            float idlePulse = Mathf.Sin(time * idlePulseSpeed) * 0.5f + 0.5f;
            
            foreach (int endpoint in endpointNodes)
            {
                if (endpoint >= colors.Length) continue;
                
                // Skip if this endpoint is currently held (handled by RenderHeldEndpointGlow)
                if (heldEndpoints.ContainsKey(endpoint)) continue;
                
                // Skip if already lit by pulse
                if (colors[endpoint].maxColorComponent > 0.5f) continue;
                
                Color endColor = endpointColors.ContainsKey(endpoint) ? endpointColors[endpoint] : endpointIdleColor;
                float intensity = idlePulseAmount + (1f - idlePulseAmount) * idlePulse * 0.3f;
                
                colors[endpoint] = Color.Lerp(inactiveColor, endColor, intensity);
            }
        }
        
        void OnEnable()
        {
            // Only reset if truly necessary (audio container was destroyed)
            // Don't reset analysisComplete here - it causes constant re-initialization in Editor
            if (audioContainer == null)
            {
                analysisComplete = false;
            }
            // Don't clear held endpoints - let them persist
        }
        
        void OnDisable()
        {
            if (subscribedToEvents)
            {
                GraphPlayerController.OnEndpointPressed -= OnEndpointPressed;
                GraphPlayerController.OnEndpointReleased -= OnEndpointReleased;
                subscribedToEvents = false;
            }
            
            // Stop all held tones
            foreach (var kvp in heldEndpoints)
            {
                if (audioSources.ContainsKey(kvp.Key))
                {
                    audioSources[kvp.Key].Stop();
                }
            }
            heldEndpoints.Clear();
            
            // Cleanup audio
            if (audioContainer != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(audioContainer);
                else
                    Object.DestroyImmediate(audioContainer);
                audioContainer = null;
            }
            audioSources.Clear();
        }
        
        // Track previous values to detect actual changes
        [System.NonSerialized] private MusicalScale lastScale;
        [System.NonSerialized] private float lastBaseFrequency;
        [System.NonSerialized] private int lastBaseOctave;
        
        void OnValidate()
        {
            // Only reset audio if musical settings actually changed
            bool musicalSettingsChanged = (lastScale != scale || 
                                           Mathf.Abs(lastBaseFrequency - baseFrequency) > 0.01f ||
                                           lastBaseOctave != baseOctave);
            
            if (musicalSettingsChanged && audioContainer != null)
            {
                // Schedule destruction for next frame (can't destroy during OnValidate)
                var toDestroy = audioContainer;
                audioContainer = null;
                analysisComplete = false;
                audioSources.Clear();
                
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () => {
                    if (toDestroy != null)
                    {
                        if (Application.isPlaying)
                            Object.Destroy(toDestroy);
                        else
                            Object.DestroyImmediate(toDestroy);
                    }
                };
                #endif
            }
            
            lastScale = scale;
            lastBaseFrequency = baseFrequency;
            lastBaseOctave = baseOctave;
        }
    }
}

