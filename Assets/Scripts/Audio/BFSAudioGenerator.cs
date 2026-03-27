using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Specialized audio generator for Breadth-First Search animations
    /// Creates ambient soundscapes with harmonic chimes that respond to graph topology
    /// </summary>
    [System.Serializable]
    public class BFSAudioGenerator
    {
        [Header("Musical Scale & Harmony")]
        [SerializeField] private MusicalScale scale = MusicalScale.Pentatonic;
        [SerializeField] private float rootFrequency = 220f; // A3 as root note
        [SerializeField] private int octaveRange = 3;
        
        [Header("Ambient Base Layer")]
        [SerializeField] private bool enableAmbientDrone = true;
        [SerializeField] private float ambientVolume = 0.3f;
        [SerializeField] private float ambientModulationSpeed = 0.1f;
        
        [Header("Chime Settings")]
        [SerializeField] private float chimeDecay = 2.0f;
        [SerializeField] private float chimeVolume = 0.4f;
        [SerializeField] private bool useHarmonicChimes = true;
        [SerializeField] private int maxHarmonics = 4;
        
        [Header("Graph Topology Mapping")]
        [SerializeField] private bool mapValenceToHarmony = true;
        [SerializeField] private bool mapDepthToReverb = true;
        [SerializeField] private float reverbIntensity = 0.3f;
        
        public enum MusicalScale
        {
            Pentatonic,     // C D E G A (very harmonious)
            Dorian,         // C D Eb F G A Bb (ambient, modal)
            Lydian,         // C D E F# G A B (dreamy, floating)
            Minor,          // C D Eb F G Ab Bb (emotional)
            Major,          // C D E F G A B (bright, happy)
            Chromatic       // All 12 tones (experimental)
        }
        
        // Audio state for BFS events
        private struct ChimeEvent
        {
            public float startTime;
            public float frequency;
            public float amplitude;
            public float decay;
            public int harmonics;
            
            public ChimeEvent(float time, float freq, float amp, float dec, int harm)
            {
                startTime = time;
                frequency = freq;
                amplitude = amp;
                decay = dec;
                harmonics = harm;
            }
        }
        
        private List<ChimeEvent> activeChimes = new List<ChimeEvent>();
        private Dictionary<int, float> nodeFrequencies = new Dictionary<int, float>();
        private float lastSearchStartTime = -1f;
        private int lastActiveSearchCount = 0;
        private float ambientPhase = 0f;
        
        /// <summary>
        /// Generate audio samples for BFS animation
        /// </summary>
        public void GenerateAudioSamples(
            float[] audioBuffer, 
            int startSample, 
            int sampleCount, 
            int sampleRate,
            BreadthFirstSearchAnimation bfsAnimation,
            LEDGraphManager graphManager,
            float currentTime)
        {
            if (bfsAnimation == null || graphManager == null) return;
            
            // Analyze current BFS state and trigger audio events
            AnalyzeBFSState(bfsAnimation, graphManager, currentTime);
            
            // Generate audio samples
            for (int i = 0; i < sampleCount; i++)
            {
                int sampleIndex = startSample + i;
                float time = (float)sampleIndex / sampleRate;
                
                float sample = 0f;
                
                // 1. Ambient drone layer
                if (enableAmbientDrone)
                {
                    sample += GenerateAmbientDrone(time);
                }
                
                // 2. Chime layer
                sample += GenerateChimes(time);
                
                // 3. Apply gentle compression to prevent clipping
                sample = TanhApproximation(sample * 0.8f) * 0.9f;
                
                // Store stereo samples (same for both channels for now)
                if (sampleIndex * 2 < audioBuffer.Length)
                {
                    audioBuffer[sampleIndex * 2] = sample;     // Left
                    audioBuffer[sampleIndex * 2 + 1] = sample; // Right
                }
            }
            
            // Clean up expired chimes
            activeChimes.RemoveAll(chime => currentTime - chime.startTime > chime.decay * 3f);
        }
        
        /// <summary>
        /// Analyze BFS animation state and trigger appropriate audio events
        /// </summary>
        private void AnalyzeBFSState(BreadthFirstSearchAnimation bfsAnimation, LEDGraphManager graphManager, float currentTime)
        {
            // This is a simplified analysis - in practice, we'd need access to the internal BFS state
            // For now, we'll create a general approach based on the animation's public interface
            
            // Map node positions to frequencies based on graph topology
            if (nodeFrequencies.Count == 0)
            {
                GenerateNodeFrequencyMap(graphManager);
            }
            
            // Simulate BFS events based on animation timing
            // This is a placeholder - ideally we'd have direct access to BFS events
            SimulateBFSAudioEvents(currentTime);
        }
        
        /// <summary>
        /// Generate frequency mapping for each node based on graph topology
        /// </summary>
        private void GenerateNodeFrequencyMap(LEDGraphManager graphManager)
        {
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
                
                // Use valence and Z position to determine frequency (Z = vertical in Rhino/Grasshopper)
                int scaleIndex = (valence + Mathf.RoundToInt(position.z * 0.1f)) % scaleFrequencies.Length;
                nodeFrequencies[i] = scaleFrequencies[scaleIndex];
            }
        }
        
        /// <summary>
        /// Generate frequencies for the selected musical scale
        /// </summary>
        private float[] GenerateScaleFrequencies()
        {
            List<float> frequencies = new List<float>();
            
            // Define scale intervals (semitones from root)
            int[] intervals = scale switch
            {
                MusicalScale.Pentatonic => new int[] { 0, 2, 4, 7, 9 },           // C D E G A
                MusicalScale.Dorian => new int[] { 0, 2, 3, 5, 7, 9, 10 },        // C D Eb F G A Bb
                MusicalScale.Lydian => new int[] { 0, 2, 4, 6, 7, 9, 11 },        // C D E F# G A B
                MusicalScale.Minor => new int[] { 0, 2, 3, 5, 7, 8, 10 },         // C D Eb F G Ab Bb
                MusicalScale.Major => new int[] { 0, 2, 4, 5, 7, 9, 11 },         // C D E F G A B
                MusicalScale.Chromatic => new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
                _ => new int[] { 0, 2, 4, 7, 9 } // Default to pentatonic
            };
            
            // Generate frequencies across octave range
            for (int octave = 0; octave < octaveRange; octave++)
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
        /// Simulate BFS audio events (placeholder for actual BFS event system)
        /// </summary>
        private void SimulateBFSAudioEvents(float currentTime)
        {
            // This is a simplified simulation - ideally we'd have direct BFS event callbacks
            
            // Trigger search start chimes periodically
            if (currentTime - lastSearchStartTime > 4f) // Every 4 seconds, simulate new search
            {
                TriggerSearchStartChime(currentTime);
                lastSearchStartTime = currentTime;
            }
            
            // Trigger node discovery chimes randomly (simulating BFS progression)
            if (Random.value < 0.1f) // 10% chance per frame to discover a node
            {
                int randomNodeIndex = Random.Range(0, nodeFrequencies.Count);
                TriggerNodeDiscoveryChime(currentTime, randomNodeIndex);
            }
        }
        
        /// <summary>
        /// Trigger a chime when a new BFS search starts
        /// </summary>
        public void TriggerSearchStartChime(float currentTime)
        {
            // Major chord for search start
            float rootFreq = rootFrequency;
            
            // Root note
            activeChimes.Add(new ChimeEvent(currentTime, rootFreq, chimeVolume * 0.8f, chimeDecay * 1.5f, 3));
            
            // Major third
            activeChimes.Add(new ChimeEvent(currentTime, rootFreq * 1.25f, chimeVolume * 0.6f, chimeDecay * 1.2f, 2));
            
            // Perfect fifth
            activeChimes.Add(new ChimeEvent(currentTime, rootFreq * 1.5f, chimeVolume * 0.4f, chimeDecay, 2));
            
            Debug.Log($"🔔 BFS Audio: Search start chime triggered at {currentTime:F2}s");
        }
        
        /// <summary>
        /// Trigger a chime when a node is discovered
        /// </summary>
        public void TriggerNodeDiscoveryChime(float currentTime, int nodeIndex)
        {
            if (!nodeFrequencies.ContainsKey(nodeIndex)) return;
            
            float frequency = nodeFrequencies[nodeIndex];
            int harmonics = useHarmonicChimes ? Random.Range(1, maxHarmonics + 1) : 1;
            
            activeChimes.Add(new ChimeEvent(
                currentTime, 
                frequency, 
                chimeVolume * Random.Range(0.3f, 0.7f), 
                chimeDecay * Random.Range(0.8f, 1.2f), 
                harmonics
            ));
        }
        
        /// <summary>
        /// Generate ambient drone layer
        /// </summary>
        private float GenerateAmbientDrone(float time)
        {
            ambientPhase += ambientModulationSpeed * Time.deltaTime;
            
            // Multiple sine waves for rich ambient texture
            float drone1 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 0.5f * time) * 0.4f;
            float drone2 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 0.75f * time) * 0.3f;
            float drone3 = Mathf.Sin(2f * Mathf.PI * rootFrequency * 1.5f * time) * 0.2f;
            
            // Slow modulation for movement
            float modulation = Mathf.Sin(ambientPhase) * 0.3f + 0.7f;
            
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
                
                // Generate fundamental frequency
                float fundamental = Mathf.Sin(2f * Mathf.PI * chime.frequency * chimeAge) * envelope;
                chimeSample += fundamental * chime.amplitude;
                
                // Add harmonics if enabled
                if (useHarmonicChimes && chime.harmonics > 1)
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
        /// Reset audio state (call when animation restarts)
        /// </summary>
        public void Reset()
        {
            activeChimes.Clear();
            nodeFrequencies.Clear();
            lastSearchStartTime = -1f;
            lastActiveSearchCount = 0;
            ambientPhase = 0f;
        }
        
        /// <summary>
        /// Approximation of hyperbolic tangent for audio compression
        /// Unity's Mathf.Tanh is not available in all versions
        /// </summary>
        private float TanhApproximation(float x)
        {
            // Fast tanh approximation: tanh(x) ≈ x / (1 + |x|) for x in [-1, 1]
            // For larger values, we clamp to prevent overflow
            x = Mathf.Clamp(x, -3f, 3f);
            
            if (Mathf.Abs(x) < 1f)
            {
                // More accurate approximation for small values
                return x / (1f + Mathf.Abs(x));
            }
            else
            {
                // For larger values, use a different approximation
                float x2 = x * x;
                return x / Mathf.Sqrt(1f + x2);
            }
        }
    }
}
