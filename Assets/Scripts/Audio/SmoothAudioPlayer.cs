using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Smooth audio player that uses pre-generated AudioClips for chimes
    /// Eliminates choppiness by avoiding real-time synthesis during playback
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SmoothAudioPlayer : MonoBehaviour
    {
        [Header("Playback Settings")]
        [SerializeField] private bool enableSmoothAudio = true;
        [SerializeField] private float masterVolume = 0.7f;
        [SerializeField] private int maxSimultaneousChimes = 8;
        
        [Header("Background Audio")]
        [SerializeField] private bool enableBackgroundAudio = true;
        [SerializeField] private float backgroundVolume = 0.2f;
        
        [Header("Animation Event Detection")]
        [SerializeField] private float eventCheckInterval = 0.05f; // Check every 50ms for smoother detection
        [SerializeField] private bool showEventDebug = false;
        
        // References
        private AudioSource backgroundAudioSource;
        private List<AudioSource> chimeAudioSources = new List<AudioSource>();
        private LEDAnimationSystem animationSystem;
        private LEDGraphManager graphManager;
        private GraphAudioMapper audioMapper;
        
        // Animation state tracking
        private Dictionary<int, bool> previousNodeStates = new Dictionary<int, bool>();
        private Dictionary<int, float> nodeActivationTimes = new Dictionary<int, float>();
        private float lastEventCheckTime = 0f;
        private bool isPlaying = false;
        
        // Pre-generated audio clips
        private Dictionary<int, AudioClip> nodeChimeClips = new Dictionary<int, AudioClip>();
        private AudioClip backgroundClip;
        
        void Start()
        {
            // Get components
            animationSystem = GetComponent<LEDAnimationSystem>();
            graphManager = GetComponent<LEDGraphManager>();
            audioMapper = GetComponent<GraphAudioMapper>();
            
            // Setup audio sources
            SetupAudioSources();
            
            Debug.Log("🎵 SmoothAudioPlayer initialized");
        }
        
        void Update()
        {
            // Update playing state
            bool shouldPlay = animationSystem != null && animationSystem.isPlaying && animationSystem.CurrentAnimation != null;
            
            if (shouldPlay && !isPlaying)
            {
                StartSmoothAudio();
            }
            else if (!shouldPlay && isPlaying)
            {
                StopSmoothAudio();
            }
            
            // Check for animation events
            if (isPlaying && enableSmoothAudio && Time.time - lastEventCheckTime > eventCheckInterval)
            {
                CheckForAnimationEvents();
                lastEventCheckTime = Time.time;
            }
        }
        
        /// <summary>
        /// Setup audio sources for smooth playback
        /// </summary>
        private void SetupAudioSources()
        {
            // Main audio source becomes background audio source
            backgroundAudioSource = GetComponent<AudioSource>();
            backgroundAudioSource.loop = true;
            backgroundAudioSource.volume = backgroundVolume;
            backgroundAudioSource.playOnAwake = false;
            
            // Create additional audio sources for chime playback
            for (int i = 0; i < maxSimultaneousChimes; i++)
            {
                GameObject chimeSourceObj = new GameObject($"ChimeAudioSource_{i}");
                chimeSourceObj.transform.SetParent(transform);
                
                AudioSource chimeSource = chimeSourceObj.AddComponent<AudioSource>();
                chimeSource.playOnAwake = false;
                chimeSource.loop = false;
                chimeSource.volume = masterVolume;
                
                chimeAudioSources.Add(chimeSource);
            }
            
            Debug.Log($"🔊 Created {chimeAudioSources.Count} chime audio sources");
        }
        
        /// <summary>
        /// Start smooth audio playback
        /// </summary>
        public void StartSmoothAudio()
        {
            if (!enableSmoothAudio) return;
            
            isPlaying = true;
            
            // Generate audio clips if needed
            if (nodeChimeClips.Count == 0)
            {
                StartCoroutine(GenerateAudioClipsCoroutine());
            }
            
            // Start background audio
            if (enableBackgroundAudio && backgroundClip != null)
            {
                backgroundAudioSource.clip = backgroundClip;
                backgroundAudioSource.Play();
            }
            
            Debug.Log("🎵 Started smooth audio playback");
        }
        
        /// <summary>
        /// Stop smooth audio playback
        /// </summary>
        public void StopSmoothAudio()
        {
            isPlaying = false;
            
            // Stop background audio
            if (backgroundAudioSource.isPlaying)
            {
                backgroundAudioSource.Stop();
            }
            
            // Stop all chime sources
            foreach (var chimeSource in chimeAudioSources)
            {
                if (chimeSource.isPlaying)
                {
                    chimeSource.Stop();
                }
            }
            
            // Clear state
            previousNodeStates.Clear();
            nodeActivationTimes.Clear();
            
            Debug.Log("🔇 Stopped smooth audio playback");
        }
        
        /// <summary>
        /// Generate audio clips for all nodes
        /// </summary>
        private IEnumerator GenerateAudioClipsCoroutine()
        {
            if (audioMapper == null || !audioMapper.IsAnalysisValid())
            {
                Debug.LogWarning("❌ GraphAudioMapper not ready. Cannot generate audio clips.");
                yield break;
            }
            
            Debug.Log("🎼 Generating audio clips for smooth playback...");
            
            var analysis = audioMapper.GetCurrentAnalysis();
            int clipCount = 0;
            
            foreach (var kvp in analysis.nodeAudioMap)
            {
                int nodeIndex = kvp.Key;
                var audioData = kvp.Value;
                
                // Generate audio clip for this node
                AudioClip clip = GenerateChimeClip(audioData);
                if (clip != null)
                {
                    nodeChimeClips[nodeIndex] = clip;
                    clipCount++;
                }
                
                // Yield occasionally to prevent frame drops
                if (clipCount % 10 == 0)
                {
                    yield return null;
                }
            }
            
            // Generate background clip
            if (enableBackgroundAudio)
            {
                backgroundClip = GenerateBackgroundClip();
            }
            
            Debug.Log($"✅ Generated {clipCount} chime clips and background audio");
        }
        
        /// <summary>
        /// Generate a chime audio clip for a specific node
        /// </summary>
        private AudioClip GenerateChimeClip(GraphAudioMapper.NodeAudioData audioData)
        {
            int sampleRate = 44100;
            float duration = audioData.audioSettings.decay + 0.5f; // Add some padding
            int samples = Mathf.RoundToInt(duration * sampleRate);
            
            float[] audioSamples = new float[samples];
            
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / sampleRate;
                float sample = GenerateChimeSample(audioData, time);
                audioSamples[i] = sample;
            }
            
            // Create AudioClip
            AudioClip clip = AudioClip.Create($"Chime_Node_{audioData.nodeIndex}", samples, 1, sampleRate, false);
            clip.SetData(audioSamples, 0);
            
            return clip;
        }
        
        /// <summary>
        /// Generate a single sample for a chime
        /// </summary>
        private float GenerateChimeSample(GraphAudioMapper.NodeAudioData audioData, float time)
        {
            var settings = audioData.audioSettings;
            
            // Exponential decay envelope
            float envelope = Mathf.Exp(-time / settings.decay);
            
            // Different envelope shapes for different chime types
            switch (settings.chimeType)
            {
                case GraphAudioMapper.ChimeType.Gong:
                    // Slower attack for gong
                    float attack = Mathf.Min(time * 3f, 1f);
                    envelope *= attack;
                    break;
                    
                case GraphAudioMapper.ChimeType.Marimba:
                    // Quick attack, faster decay
                    envelope = Mathf.Exp(-time / (settings.decay * 0.7f));
                    break;
            }
            
            float sample = 0f;
            
            // Generate fundamental and harmonics
            for (int h = 1; h <= settings.harmonics; h++)
            {
                float harmonicFreq = audioData.frequency * h;
                float harmonicAmp = settings.volume / (h * h); // Decreasing harmonic intensity
                
                // Different harmonic patterns for different chime types
                float harmonicSample = Mathf.Sin(2f * Mathf.PI * harmonicFreq * time);
                
                switch (settings.chimeType)
                {
                    case GraphAudioMapper.ChimeType.Crystal:
                        // Pure harmonics
                        harmonicAmp = settings.volume / h;
                        break;
                        
                    case GraphAudioMapper.ChimeType.Gong:
                        // Complex inharmonic partials
                        harmonicFreq = audioData.frequency * (h + 0.1f * h * h);
                        break;
                }
                
                sample += harmonicSample * harmonicAmp * envelope;
            }
            
            return Mathf.Clamp(sample, -1f, 1f);
        }
        
        /// <summary>
        /// Generate background audio clip
        /// </summary>
        private AudioClip GenerateBackgroundClip()
        {
            if (audioMapper == null) return null;
            
            int sampleRate = 44100;
            float duration = 30f; // 30 second loop
            int samples = Mathf.RoundToInt(duration * sampleRate);
            
            float[] audioSamples = new float[samples];
            var analysis = audioMapper.GetCurrentAnalysis();
            
            // Use the scale frequencies for background harmony
            float[] scaleFreqs = analysis.scaleFrequencies;
            if (scaleFreqs == null || scaleFreqs.Length == 0) return null;
            
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / sampleRate;
                float sample = 0f;
                
                // Multiple drone layers using scale frequencies
                for (int j = 0; j < Mathf.Min(3, scaleFreqs.Length); j++)
                {
                    float freq = scaleFreqs[j] * 0.5f; // Lower octave for background
                    float amplitude = 0.1f / (j + 1); // Decreasing amplitude
                    
                    sample += Mathf.Sin(2f * Mathf.PI * freq * time) * amplitude;
                }
                
                // Slow modulation
                float modulation = Mathf.Sin(2f * Mathf.PI * 0.1f * time) * 0.3f + 0.7f;
                sample *= modulation;
                
                audioSamples[i] = Mathf.Clamp(sample * backgroundVolume, -1f, 1f);
            }
            
            AudioClip clip = AudioClip.Create("BackgroundAudio", samples, 1, sampleRate, false);
            clip.SetData(audioSamples, 0);
            
            return clip;
        }
        
        /// <summary>
        /// Check for animation events and trigger audio
        /// </summary>
        private void CheckForAnimationEvents()
        {
            if (graphManager?.CurrentNodeColors == null) return;
            
            var currentColors = graphManager.CurrentNodeColors;
            var nodePositions = graphManager.NodePositions;
            
            // Track which nodes are currently active
            Dictionary<int, bool> currentNodeStates = new Dictionary<int, bool>();
            
            for (int i = 0; i < currentColors.Length && i < nodePositions.Count; i++)
            {
                Color nodeColor = currentColors[i];
                Color inactiveColor = Color.black;
                
                // Consider a node "active" if it's significantly different from inactive
                bool isActive = Vector3.Distance(new Vector3(nodeColor.r, nodeColor.g, nodeColor.b), 
                                                new Vector3(inactiveColor.r, inactiveColor.g, inactiveColor.b)) > 0.1f;
                
                currentNodeStates[i] = isActive;
            }
            
            // Detect newly activated nodes
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
        }
        
        /// <summary>
        /// Trigger a chime for a specific node using pre-generated clips
        /// </summary>
        private void TriggerNodeChime(int nodeIndex)
        {
            if (!nodeChimeClips.ContainsKey(nodeIndex))
            {
                if (showEventDebug)
                    Debug.LogWarning($"⚠️ No audio clip for node {nodeIndex}");
                return;
            }
            
            // Find an available audio source
            AudioSource availableSource = null;
            foreach (var source in chimeAudioSources)
            {
                if (!source.isPlaying)
                {
                    availableSource = source;
                    break;
                }
            }
            
            if (availableSource == null)
            {
                // All sources busy, use the first one (oldest sound)
                availableSource = chimeAudioSources[0];
            }
            
            // Play the chime
            AudioClip chimeClip = nodeChimeClips[nodeIndex];
            availableSource.clip = chimeClip;
            availableSource.volume = masterVolume;
            availableSource.Play();
            
            // Track activation time
            nodeActivationTimes[nodeIndex] = Time.time;
            
            if (showEventDebug)
            {
                var audioData = audioMapper?.GetNodeAudioData(nodeIndex);
                string nodeType = audioData?.isSourceNode == true ? "SOURCE" : 
                                 audioData?.isHighValence == true ? "HIGH-VALENCE" : 
                                 audioData?.isEndpoint == true ? "ENDPOINT" : "REGULAR";
                
                Debug.Log($"🔔 Triggered {nodeType} chime: Node {nodeIndex} ({audioData?.frequency:F1}Hz)");
            }
        }
        
        /// <summary>
        /// Regenerate audio clips when audio mapper settings change
        /// </summary>
        [ContextMenu("Regenerate Audio Clips")]
        public void RegenerateAudioClips()
        {
            // Clear existing clips
            foreach (var clip in nodeChimeClips.Values)
            {
                if (clip != null)
                    DestroyImmediate(clip);
            }
            nodeChimeClips.Clear();
            
            if (backgroundClip != null)
            {
                DestroyImmediate(backgroundClip);
                backgroundClip = null;
            }
            
            // Regenerate
            if (isPlaying)
            {
                StartCoroutine(GenerateAudioClipsCoroutine());
            }
            
            Debug.Log("🔄 Audio clips regenerated");
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Test All Node Chimes")]
        public void TestAllNodeChimes()
        {
            if (nodeChimeClips.Count == 0)
            {
                Debug.LogWarning("❌ No audio clips generated yet");
                return;
            }
            
            StartCoroutine(TestChimesCoroutine());
        }
        
        private IEnumerator TestChimesCoroutine()
        {
            Debug.Log("🔔 Testing all node chimes...");
            
            int testCount = Mathf.Min(10, nodeChimeClips.Count);
            int tested = 0;
            
            foreach (var kvp in nodeChimeClips)
            {
                if (tested >= testCount) break;
                
                int nodeIndex = kvp.Key;
                TriggerNodeChime(nodeIndex);
                
                tested++;
                yield return new WaitForSeconds(0.3f); // Space out the tests
            }
            
            Debug.Log($"✅ Tested {tested} node chimes");
        }
        #endif
    }
}
