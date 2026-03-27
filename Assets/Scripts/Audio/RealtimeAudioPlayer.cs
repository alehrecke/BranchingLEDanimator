using UnityEngine;
using System.Collections.Generic;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Real-time audio player that generates audio live during LED animations
    /// Uses Unity's AudioSource and OnAudioFilterRead for real-time synthesis
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RealtimeAudioPlayer : MonoBehaviour
    {
        [Header("Real-time Audio Settings")]
        [Tooltip("Enable this for continuous audio that follows the LED animation. Disable when using interactive animations with their own audio (like ChordTouch).")]
        [SerializeField] private bool enableRealtimeAudio = false; // Disabled by default - enable manually when needed
        [SerializeField] private float masterVolume = 0.5f;
        
        [Header("BFS Event Audio")]
        [SerializeField] private bool enableBFSEventAudio = true;
        [SerializeField] private BFSAudioGenerator.MusicalScale musicalScale = BFSAudioGenerator.MusicalScale.Pentatonic;
        [SerializeField] private float rootFrequency = 220f; // A3
        
        [Header("Chime Settings")]
        [SerializeField] private float chimeVolume = 0.6f;
        [SerializeField] private float chimeDecay = 1.5f;
        [SerializeField] private bool enableHarmonics = true;
        [SerializeField] private int maxHarmonics = 3;
        
        [Header("Ambient Drone")]
        [SerializeField] private bool enableAmbientDrone = true;
        [SerializeField] private float ambientVolume = 0.2f;
        [SerializeField] private float ambientModulationSpeed = 0.05f;
        
        // References
        private AudioSource audioSource;
        private LEDAnimationSystem animationSystem;
        private LEDGraphManager graphManager;
        private BFSAudioGenerator bfsAudioGenerator;
        
        // Real-time audio state
        private List<ChimeEvent> activeChimes = new List<ChimeEvent>();
        private Dictionary<int, float> nodeFrequencies = new Dictionary<int, float>();
        private int sampleRate;
        
        // BFS state tracking for event triggering
        private Dictionary<int, bool> previousNodeStates = new Dictionary<int, bool>();
        private int lastActiveNodeCount = 0;
        private float lastEventCheckTime = 0f;
        private const float eventCheckInterval = 0.1f; // Check for events every 100ms
        
        // Thread-safe audio state
        private volatile bool shouldGenerateAudio = false;
        private volatile float audioStartTime = 0.0f;
        private volatile float lastMainThreadTime = 0.0f;
        private volatile int totalSamplesProcessed = 0;
        
        private struct ChimeEvent
        {
            public float startTime;
            public float frequency;
            public float amplitude;
            public float decay;
            public int harmonics;
            public bool isHighValence; // Special flag for high-valence nodes
            
            public ChimeEvent(float time, float freq, float amp, float dec, int harm, bool highVal = false)
            {
                startTime = time;
                frequency = freq;
                amplitude = amp;
                decay = dec;
                harmonics = harm;
                isHighValence = highVal;
            }
        }
        
        void Start()
        {
            // Get components
            audioSource = GetComponent<AudioSource>();
            animationSystem = GetComponent<LEDAnimationSystem>();
            graphManager = GetComponent<LEDGraphManager>();
            
            // Configure AudioSource for real-time synthesis
            audioSource.clip = null; // No clip - we generate in real-time
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.volume = masterVolume;
            
            // Get audio settings
            sampleRate = AudioSettings.outputSampleRate;
            
            // Initialize BFS audio generator
            bfsAudioGenerator = new BFSAudioGenerator();
            
            Debug.Log($"🎵 RealtimeAudioPlayer initialized - Sample Rate: {sampleRate}Hz");
            
            // Generate node frequency mapping
            if (graphManager != null && graphManager.DataLoaded)
            {
                GenerateNodeFrequencyMapping();
            }
        }
        
        void Update()
        {
            // Update thread-safe time tracking
            lastMainThreadTime = (float)Time.time;
            
            // Check if we should enable/disable audio based on animation state
            if (animationSystem != null && enableRealtimeAudio)
            {
                bool shouldPlay = animationSystem.isPlaying && animationSystem.CurrentAnimation != null;
                
                if (shouldPlay && !shouldGenerateAudio)
                {
                    StartRealtimeAudio();
                }
                else if (!shouldPlay && shouldGenerateAudio)
                {
                    StopRealtimeAudio();
                }
                
                // Check for BFS events periodically
                if (shouldPlay && Time.time - lastEventCheckTime > eventCheckInterval)
                {
                    CheckForBFSEvents();
                    lastEventCheckTime = Time.time;
                }
            }
        }
        
        /// <summary>
        /// Start real-time audio playback
        /// </summary>
        public void StartRealtimeAudio()
        {
            // Thread-safe check - only access audioSource from main thread
            if (!shouldGenerateAudio)
            {
                // Generate frequency mapping if not already done
                if (nodeFrequencies.Count == 0 && graphManager != null && graphManager.DataLoaded)
                {
                    GenerateNodeFrequencyMapping();
                }
                
                shouldGenerateAudio = true;
                audioStartTime = (float)Time.time; // Capture start time on main thread
                totalSamplesProcessed = 0; // Reset sample counter
                audioSource.Play();
                Debug.Log("🎵 Started real-time audio playback");
            }
        }
        
        /// <summary>
        /// Stop real-time audio playback
        /// </summary>
        public void StopRealtimeAudio()
        {
            shouldGenerateAudio = false;
            // Thread-safe - only access audioSource from main thread
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                activeChimes.Clear();
                previousNodeStates.Clear();
                Debug.Log("🔇 Stopped real-time audio playback");
            }
        }
        
        /// <summary>
        /// Generate frequency mapping for each node based on graph topology
        /// </summary>
        private void GenerateNodeFrequencyMapping()
        {
            if (graphManager == null || !graphManager.DataLoaded) return;
            
            nodeFrequencies.Clear();
            
            var nodePositions = graphManager.NodePositions;
            var edgeConnections = graphManager.EdgeConnections;
            
            // Calculate node valences (connection counts)
            Dictionary<int, int> nodeValences = new Dictionary<int, int>();
            for (int i = 0; i < nodePositions.Count; i++)
            {
                nodeValences[i] = 0;
            }
            
            foreach (var edge in edgeConnections)
            {
                if (nodeValences.ContainsKey(edge.x)) nodeValences[edge.x]++;
                if (nodeValences.ContainsKey(edge.y)) nodeValences[edge.y]++;
            }
            
            // Generate scale frequencies
            float[] scaleFrequencies = GenerateScaleFrequencies();
            
            // Map nodes to frequencies based on valence and position
            for (int i = 0; i < nodePositions.Count; i++)
            {
                int valence = nodeValences.ContainsKey(i) ? nodeValences[i] : 0;
                Vector3 position = nodePositions[i];
                
                // Use valence as primary factor, Y position as secondary
                int scaleIndex = (valence * 2 + Mathf.RoundToInt(position.y * 0.05f)) % scaleFrequencies.Length;
                nodeFrequencies[i] = scaleFrequencies[scaleIndex];
                
                // Debug high-valence nodes
                if (valence > 2)
                {
                    Debug.Log($"🔔 High-valence node {i}: valence={valence}, frequency={nodeFrequencies[i]:F1}Hz");
                }
            }
            
            Debug.Log($"🎼 Generated frequency mapping for {nodeFrequencies.Count} nodes using {musicalScale} scale");
        }
        
        /// <summary>
        /// Generate frequencies for the selected musical scale
        /// </summary>
        private float[] GenerateScaleFrequencies()
        {
            List<float> frequencies = new List<float>();
            
            // Define scale intervals (semitones from root)
            int[] intervals = musicalScale switch
            {
                BFSAudioGenerator.MusicalScale.Pentatonic => new int[] { 0, 2, 4, 7, 9 },           // C D E G A
                BFSAudioGenerator.MusicalScale.Dorian => new int[] { 0, 2, 3, 5, 7, 9, 10 },        // C D Eb F G A Bb
                BFSAudioGenerator.MusicalScale.Lydian => new int[] { 0, 2, 4, 6, 7, 9, 11 },        // C D E F# G A B
                BFSAudioGenerator.MusicalScale.Minor => new int[] { 0, 2, 3, 5, 7, 8, 10 },         // C D Eb F G Ab Bb
                BFSAudioGenerator.MusicalScale.Major => new int[] { 0, 2, 4, 5, 7, 9, 11 },         // C D E F G A B
                BFSAudioGenerator.MusicalScale.Chromatic => new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                _ => new int[] { 0, 2, 4, 7, 9 } // Default to pentatonic
            };
            
            // Generate frequencies across 3 octaves
            for (int octave = 0; octave < 3; octave++)
            {
                foreach (int interval in intervals)
                {
                    float frequency = rootFrequency * Mathf.Pow(2f, octave + interval / 12f);
                    frequencies.Add(frequency);
                }
            }
            
            return frequencies.ToArray();
        }
        
        /// <summary>
        /// Check for BFS animation events and trigger audio
        /// </summary>
        private void CheckForBFSEvents()
        {
            if (animationSystem?.CurrentAnimation == null || graphManager?.CurrentNodeColors == null) return;
            
            var currentColors = graphManager.CurrentNodeColors;
            var nodePositions = graphManager.NodePositions;
            
            // Track which nodes are currently active (not inactive color)
            Dictionary<int, bool> currentNodeStates = new Dictionary<int, bool>();
            int currentActiveCount = 0;
            
            for (int i = 0; i < currentColors.Length && i < nodePositions.Count; i++)
            {
                Color nodeColor = currentColors[i];
                Color inactiveColor = Color.black; // Assuming black is inactive - adjust as needed
                
                // Consider a node "active" if it's significantly different from inactive color
                bool isActive = Vector3.Distance(new Vector3(nodeColor.r, nodeColor.g, nodeColor.b), 
                                                new Vector3(inactiveColor.r, inactiveColor.g, inactiveColor.b)) > 0.1f;
                
                currentNodeStates[i] = isActive;
                if (isActive) currentActiveCount++;
            }
            
            // Detect newly activated nodes (chime triggers)
            foreach (var kvp in currentNodeStates)
            {
                int nodeIndex = kvp.Key;
                bool isCurrentlyActive = kvp.Value;
                bool wasPreviouslyActive = previousNodeStates.ContainsKey(nodeIndex) && previousNodeStates[nodeIndex];
                
                // Trigger chime for newly activated nodes
                if (isCurrentlyActive && !wasPreviouslyActive)
                {
                    TriggerNodeChime(nodeIndex);
                }
            }
            
            // Store current state for next frame
            previousNodeStates = currentNodeStates;
            lastActiveNodeCount = currentActiveCount;
        }
        
        /// <summary>
        /// Trigger a chime for a specific node
        /// </summary>
        private void TriggerNodeChime(int nodeIndex)
        {
            if (!nodeFrequencies.ContainsKey(nodeIndex)) return;
            
            float frequency = nodeFrequencies[nodeIndex];
            float currentTime = lastMainThreadTime; // Use thread-safe time
            
            // Calculate node valence for special effects
            int valence = CalculateNodeValence(nodeIndex);
            bool isHighValence = valence > 2; // Endpoints and junctions
            
            // Create chime with properties based on valence
            float amplitude = isHighValence ? chimeVolume * 1.2f : chimeVolume * 0.8f;
            float decay = isHighValence ? chimeDecay * 1.3f : chimeDecay;
            int harmonics = isHighValence && enableHarmonics ? maxHarmonics : 1;
            
            var chime = new ChimeEvent(currentTime, frequency, amplitude, decay, harmonics, isHighValence);
            activeChimes.Add(chime);
            
            // Debug output
            string valenceType = isHighValence ? "HIGH-VALENCE" : "regular";
            Debug.Log($"🔔 Triggered {valenceType} chime: Node {nodeIndex}, {frequency:F1}Hz, valence={valence}");
        }
        
        /// <summary>
        /// Calculate valence (connection count) for a node
        /// </summary>
        private int CalculateNodeValence(int nodeIndex)
        {
            if (graphManager?.EdgeConnections == null) return 0;
            
            int valence = 0;
            foreach (var edge in graphManager.EdgeConnections)
            {
                if (edge.x == nodeIndex || edge.y == nodeIndex)
                {
                    valence++;
                }
            }
            return valence;
        }
        
        /// <summary>
        /// Unity's audio callback - generates audio in real-time
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!enableRealtimeAudio || !shouldGenerateAudio) return;
            
            int sampleCount = data.Length / channels;
            // Calculate current time based on samples processed (thread-safe)
            float audioElapsedTime = (float)totalSamplesProcessed / sampleRate;
            float currentTime = audioStartTime + audioElapsedTime;
            
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = 0f;
                float time = currentTime + (float)i / sampleRate;
                
                // 1. Ambient drone layer
                if (enableAmbientDrone)
                {
                    sample += GenerateAmbientDrone(time);
                }
                
                // 2. Active chimes
                sample += GenerateChimes(time);
                
                // 3. Apply volume and gentle compression
                sample = TanhApproximation(sample * masterVolume * 0.8f) * 0.9f;
                
                // Store in all channels
                for (int channel = 0; channel < channels; channel++)
                {
                    data[i * channels + channel] = sample;
                }
            }
            
            // Update sample counter (thread-safe)
            totalSamplesProcessed += sampleCount;
            
            // Clean up expired chimes (using thread-safe time)
            activeChimes.RemoveAll(chime => currentTime - chime.startTime > chime.decay * 3f);
        }
        
        /// <summary>
        /// Generate ambient drone layer
        /// </summary>
        private float GenerateAmbientDrone(float time)
        {
            // Use time directly for modulation instead of deltaTime (thread-safe)
            float modulationPhase = time * ambientModulationSpeed;
            
            // Multiple sine waves for rich ambient texture
            float drone1 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 0.5f * time) * 0.4f;
            float drone2 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 0.75f * time) * 0.3f;
            float drone3 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 1.5f * time) * 0.2f;
            
            // Slow modulation for movement (thread-safe)
            float modulation = Mathf.Sin(modulationPhase) * 0.3f + 0.7f;
            
            return (drone1 + drone2 + drone3) * ambientVolume * modulation;
        }
        
        /// <summary>
        /// Generate chime layer with harmonics and decay
        /// </summary>
        private float GenerateChimes(float time)
        {
            float chimeSample = 0f;
            
            foreach (var chime in activeChimes)
            {
                float chimeAge = time - chime.startTime;
                if (chimeAge < 0 || chimeAge > chime.decay * 3f) continue;
                
                // Exponential decay envelope
                float envelope = Mathf.Exp(-chimeAge / chime.decay);
                
                // Special envelope for high-valence nodes (longer attack, more resonant)
                if (chime.isHighValence)
                {
                    float attack = Mathf.Min(chimeAge * 4f, 1f); // Slower attack
                    envelope *= attack;
                }
                
                // Generate fundamental frequency
                float fundamental = Mathf.Sin(2f * Mathf.PI * chime.frequency * chimeAge) * envelope;
                chimeSample += fundamental * chime.amplitude;
                
                // Add harmonics if enabled
                if (enableHarmonics && chime.harmonics > 1)
                {
                    for (int h = 2; h <= chime.harmonics; h++)
                    {
                        float harmonic = Mathf.Sin(2f * Mathf.PI * chime.frequency * h * chimeAge) * envelope;
                        chimeSample += harmonic * chime.amplitude / (h * h); // Decreasing harmonic intensity
                    }
                }
            }
            
            return chimeSample;
        }
        
        /// <summary>
        /// Approximation of hyperbolic tangent for audio compression
        /// </summary>
        private float TanhApproximation(float x)
        {
            x = Mathf.Clamp(x, -3f, 3f);
            
            if (Mathf.Abs(x) < 1f)
            {
                return x / (1f + Mathf.Abs(x));
            }
            else
            {
                float x2 = x * x;
                return x / Mathf.Sqrt(1f + x2);
            }
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Test High-Valence Chime")]
        public void TestHighValenceChime()
        {
            TriggerNodeChime(0); // Test with first node
        }
        
        [ContextMenu("Test Multiple Chimes")]
        public void TestMultipleChimes()
        {
            for (int i = 0; i < 5; i++)
            {
                TriggerNodeChime(i);
            }
        }
        #endif
    }
}
