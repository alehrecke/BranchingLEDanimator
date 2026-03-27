using UnityEngine;
using System.Collections.Generic;
using System.IO;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Generates procedural audio to match LED animations and exports to ESP32-compatible format
    /// Integrates with existing LED animation system for synchronized audio+visual experiences
    /// </summary>
    public class LEDAudioGenerator : MonoBehaviour
    {
        [Header("Audio Generation Settings")]
        [SerializeField] private int sampleRate = 44100;
        [SerializeField] private int channels = 2; // Stereo
        [SerializeField] private int bitDepth = 16;
        
        [Header("Audio Style")]
        [SerializeField] private AudioGenerationMode generationMode = AudioGenerationMode.Harmonic;
        [SerializeField] private float baseFrequency = 220f; // A3 note
        [SerializeField] private float volume = 0.5f;
        
        [Header("BFS Audio (Specialized)")]
        [SerializeField] private BFSAudioGenerator bfsAudioGenerator = new BFSAudioGenerator();
        
        [Header("Animation Synchronization")]
        [SerializeField] private bool syncWithAnimation = true;
        [SerializeField] private float beatFrequency = 1.0f; // Beats per second
        
        [Header("Export Settings")]
        [SerializeField] private string exportPath = "AudioExports";
        [SerializeField] private bool autoExportOnGenerate = true;
        
        // References to existing LED system
        private LEDAnimationSystem animationSystem;
        private LEDGraphManager graphManager;
        
        // Audio generation data
        private float[] audioData;
        private float currentTime;
        private int totalSamples;
        
        public enum AudioGenerationMode
        {
            Harmonic,      // Musical tones based on LED patterns
            Ambient,       // Atmospheric soundscapes
            Rhythmic,      // Beat-based patterns
            Reactive,      // Responds to LED color changes
            Cinematic,     // Dramatic audio for presentations
            BFS_Specialized // Specialized audio for Breadth-First Search animations
        }
        
        void Start()
        {
            // Get references to existing LED system components
            animationSystem = GetComponent<LEDAnimationSystem>();
            graphManager = GetComponent<LEDGraphManager>();
            
            // Create export directory
            CreateExportDirectory();
            
            Debug.Log("🎵 LEDAudioGenerator initialized - Ready to create synchronized audio!");
        }
        
        /// <summary>
        /// Generate audio for the currently active LED animation
        /// </summary>
        public void GenerateAudioForCurrentAnimation()
        {
            if (animationSystem == null || animationSystem.CurrentAnimation == null)
            {
                Debug.LogWarning("❌ No LED animation active - cannot generate audio");
                return;
            }
            
            if (graphManager == null || graphManager.NodePositions.Count == 0)
            {
                Debug.LogWarning("❌ No LED geometry data - cannot generate audio");
                return;
            }
            
            Debug.Log("🎵 Generating audio for animation: " + animationSystem.CurrentAnimation.name);
            
            // Calculate animation duration and audio length
            float animationDuration = CalculateAnimationDuration();
            GenerateAudioData(animationDuration);
            
            if (autoExportOnGenerate)
            {
                string filename = animationSystem.CurrentAnimation.name + "_audio.wav";
                ExportToWAVFile(filename);
            }
        }
        
        /// <summary>
        /// Generate audio for a specific animation with custom duration
        /// </summary>
        public void GenerateAudioForAnimation(LEDAnimationType animation, float duration)
        {
            if (animation == null)
            {
                Debug.LogWarning("❌ Animation is null - cannot generate audio");
                return;
            }
            
            Debug.Log($"🎵 Generating {duration:F1}s audio for: {animation.name}");
            
            // Store current animation index and temporarily switch
            int previousIndex = animationSystem?.currentAnimationIndex ?? 0;
            if (animationSystem != null)
            {
                // Find the animation in the available animations list
                int animationIndex = animationSystem.availableAnimations.IndexOf(animation);
                if (animationIndex >= 0)
                {
                    animationSystem.currentAnimationIndex = animationIndex;
                }
            }
            
            GenerateAudioData(duration);
            
            // Restore previous animation index
            if (animationSystem != null)
            {
                animationSystem.currentAnimationIndex = previousIndex;
            }
            
            if (autoExportOnGenerate)
            {
                string filename = animation.name + "_audio.wav";
                ExportToWAVFile(filename);
            }
        }
        
        /// <summary>
        /// Generate the actual audio data based on LED animation patterns
        /// </summary>
        private void GenerateAudioData(float duration)
        {
            totalSamples = Mathf.RoundToInt(duration * sampleRate);
            audioData = new float[totalSamples * channels];
            
            Debug.Log($"📊 Generating {totalSamples} samples ({duration:F1}s) at {sampleRate}Hz");
            
            // Generate audio based on selected mode
            switch (generationMode)
            {
                case AudioGenerationMode.Harmonic:
                    GenerateHarmonicAudio(duration);
                    break;
                case AudioGenerationMode.Ambient:
                    GenerateAmbientAudio(duration);
                    break;
                case AudioGenerationMode.Rhythmic:
                    GenerateRhythmicAudio(duration);
                    break;
                case AudioGenerationMode.Reactive:
                    GenerateReactiveAudio(duration);
                    break;
                case AudioGenerationMode.Cinematic:
                    GenerateCinematicAudio(duration);
                    break;
                case AudioGenerationMode.BFS_Specialized:
                    GenerateBFSSpecializedAudio(duration);
                    break;
            }
            
            Debug.Log("✅ Audio generation complete!");
        }
        
        /// <summary>
        /// Generate harmonic audio based on LED node positions and colors
        /// </summary>
        private void GenerateHarmonicAudio(float duration)
        {
            for (int sample = 0; sample < totalSamples; sample++)
            {
                currentTime = (float)sample / sampleRate;
                float normalizedTime = currentTime / duration;
                
                // Get LED colors at this time point
                Color[] ledColors = GetLEDColorsAtTime(normalizedTime);
                
                // Generate harmonics based on LED colors and positions
                float leftChannel = 0f;
                float rightChannel = 0f;
                
                if (ledColors != null && graphManager != null)
                {
                    for (int i = 0; i < Mathf.Min(ledColors.Length, graphManager.NodePositions.Count); i++)
                    {
                        Color ledColor = ledColors[i];
                        Vector3 position = graphManager.NodePositions[i];
                        
                        // Convert LED properties to audio parameters
                        float frequency = baseFrequency + (ledColor.r * 200f) + (ledColor.g * 300f) + (ledColor.b * 400f);
                        float amplitude = (ledColor.r + ledColor.g + ledColor.b) / 3f * volume * 0.1f; // Low volume per LED
                        
                        // Spatial audio: use X position for stereo panning
                        float pan = Mathf.Clamp01((position.x + 50f) / 100f); // Adjust range as needed
                        
                        // Generate sine wave
                        float wave = Mathf.Sin(2f * Mathf.PI * frequency * currentTime) * amplitude;
                        
                        leftChannel += wave * (1f - pan);
                        rightChannel += wave * pan;
                    }
                }
                
                // Add base rhythm
                float beat = Mathf.Sin(2f * Mathf.PI * beatFrequency * currentTime) * 0.1f;
                leftChannel += beat;
                rightChannel += beat;
                
                // Store stereo samples
                audioData[sample * 2] = Mathf.Clamp(leftChannel, -1f, 1f);     // Left
                audioData[sample * 2 + 1] = Mathf.Clamp(rightChannel, -1f, 1f); // Right
            }
        }
        
        /// <summary>
        /// Generate ambient atmospheric audio
        /// </summary>
        private void GenerateAmbientAudio(float duration)
        {
            for (int sample = 0; sample < totalSamples; sample++)
            {
                currentTime = (float)sample / sampleRate;
                
                // Multiple sine waves for ambient texture
                float wave1 = Mathf.Sin(2f * Mathf.PI * (baseFrequency * 0.5f) * currentTime) * 0.3f;
                float wave2 = Mathf.Sin(2f * Mathf.PI * (baseFrequency * 0.75f) * currentTime) * 0.2f;
                float wave3 = Mathf.Sin(2f * Mathf.PI * (baseFrequency * 1.25f) * currentTime) * 0.1f;
                
                // Add slow modulation
                float modulation = Mathf.Sin(2f * Mathf.PI * 0.1f * currentTime) * 0.5f + 0.5f;
                
                float sample_value = (wave1 + wave2 + wave3) * modulation * volume;
                
                audioData[sample * 2] = sample_value;     // Left
                audioData[sample * 2 + 1] = sample_value; // Right (mono for ambient)
            }
        }
        
        /// <summary>
        /// Generate rhythmic beat-based audio
        /// </summary>
        private void GenerateRhythmicAudio(float duration)
        {
            for (int sample = 0; sample < totalSamples; sample++)
            {
                currentTime = (float)sample / sampleRate;
                
                // Main beat
                float beat = Mathf.Sin(2f * Mathf.PI * beatFrequency * currentTime);
                float kick = beat > 0 ? Mathf.Pow(beat, 0.3f) : 0f;
                
                // Hi-hat (higher frequency)
                float hihat = Mathf.Sin(2f * Mathf.PI * beatFrequency * 4f * currentTime) * 0.3f;
                
                // Bass line
                float bass = Mathf.Sin(2f * Mathf.PI * baseFrequency * currentTime) * 0.4f;
                
                float sample_value = (kick * 0.6f + hihat + bass) * volume;
                
                audioData[sample * 2] = sample_value;     // Left
                audioData[sample * 2 + 1] = sample_value; // Right
            }
        }
        
        /// <summary>
        /// Generate reactive audio that responds to LED color changes
        /// </summary>
        private void GenerateReactiveAudio(float duration)
        {
            // Similar to harmonic but with more dramatic responses to color changes
            GenerateHarmonicAudio(duration);
            
            // Add reactive elements (this is a simplified version)
            for (int sample = 0; sample < totalSamples; sample++)
            {
                currentTime = (float)sample / sampleRate;
                float normalizedTime = currentTime / duration;
                
                // Get color intensity
                Color[] ledColors = GetLEDColorsAtTime(normalizedTime);
                float totalIntensity = 0f;
                
                if (ledColors != null)
                {
                    foreach (Color color in ledColors)
                    {
                        totalIntensity += color.r + color.g + color.b;
                    }
                    totalIntensity /= ledColors.Length * 3f; // Normalize
                }
                
                // Add intensity-based effects
                float effect = Mathf.Sin(2f * Mathf.PI * baseFrequency * 2f * currentTime) * totalIntensity * 0.2f;
                
                audioData[sample * 2] += effect;
                audioData[sample * 2 + 1] += effect;
            }
        }
        
        /// <summary>
        /// Generate cinematic dramatic audio
        /// </summary>
        private void GenerateCinematicAudio(float duration)
        {
            for (int sample = 0; sample < totalSamples; sample++)
            {
                currentTime = (float)sample / sampleRate;
                float progress = currentTime / duration;
                
                // Build intensity over time
                float intensity = Mathf.Pow(progress, 2f);
                
                // Multiple orchestral-like layers
                float strings = Mathf.Sin(2f * Mathf.PI * baseFrequency * currentTime) * intensity * 0.4f;
                float brass = Mathf.Sin(2f * Mathf.PI * baseFrequency * 1.5f * currentTime) * intensity * 0.3f;
                float timpani = Mathf.Sin(2f * Mathf.PI * baseFrequency * 0.5f * currentTime) * intensity * 0.2f;
                
                float sample_value = (strings + brass + timpani) * volume;
                
                audioData[sample * 2] = sample_value;
                audioData[sample * 2 + 1] = sample_value;
            }
        }
        
        /// <summary>
        /// Generate specialized audio for Breadth-First Search animations
        /// Creates ambient soundscapes with harmonic chimes responding to graph topology
        /// </summary>
        private void GenerateBFSSpecializedAudio(float duration)
        {
            if (animationSystem?.CurrentAnimation == null)
            {
                Debug.LogWarning("❌ No current animation for BFS audio generation");
                return;
            }
            
            // Check if current animation is actually a BFS animation
            var bfsAnimation = animationSystem.CurrentAnimation as BreadthFirstSearchAnimation;
            if (bfsAnimation == null)
            {
                Debug.LogWarning("⚠️ Current animation is not a BreadthFirstSearchAnimation. Using Ambient mode instead.");
                GenerateAmbientAudio(duration);
                return;
            }
            
            Debug.Log("🎵 Generating specialized BFS audio with harmonic chimes and ambient layers");
            
            // Reset the BFS audio generator
            bfsAudioGenerator.Reset();
            
            // Generate audio using the specialized BFS generator
            bfsAudioGenerator.GenerateAudioSamples(
                audioData, 
                0, 
                totalSamples, 
                sampleRate, 
                bfsAnimation, 
                graphManager, 
                0f
            );
            
            Debug.Log("✅ BFS specialized audio generation complete");
        }
        
        /// <summary>
        /// Get LED colors at a specific time point in the animation
        /// </summary>
        private Color[] GetLEDColorsAtTime(float normalizedTime)
        {
            if (animationSystem?.CurrentAnimation == null || graphManager?.NodePositions == null)
                return null;
                
            // Calculate frame number based on time
            int frame = Mathf.RoundToInt(normalizedTime * 30f); // Assume 30 FPS
            
            // Get colors from animation system
            return animationSystem.CurrentAnimation.CalculateNodeColors(
                graphManager.NodePositions,
                graphManager.EdgeConnections,
                graphManager.SourceNodes,
                normalizedTime * 10f, // Convert to animation time
                frame
            );
        }
        
        /// <summary>
        /// Calculate the duration of the current animation
        /// </summary>
        private float CalculateAnimationDuration()
        {
            // Default duration - could be made configurable or read from animation
            return 10f; // 10 seconds default
        }
        
        /// <summary>
        /// Export generated audio to WAV file format compatible with ESP32
        /// </summary>
        public void ExportToWAVFile(string filename)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Debug.LogWarning("❌ No audio data to export");
                return;
            }
            
            string fullPath = Path.Combine(Application.dataPath, exportPath, filename);
            
            try
            {
                WriteWAVFile(fullPath, audioData, sampleRate, channels);
                Debug.Log($"✅ Audio exported: {fullPath}");
                Debug.Log($"📊 File info: {audioData.Length / channels} samples, {sampleRate}Hz, {channels} channels");
                
                #if UNITY_EDITOR
                AssetDatabase.Refresh();
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Failed to export audio: {e.Message}");
            }
        }
        
        /// <summary>
        /// Write WAV file in ESP32-compatible format
        /// </summary>
        private void WriteWAVFile(string filepath, float[] audioData, int sampleRate, int channels)
        {
            int samples = audioData.Length / channels;
            int bytesPerSample = bitDepth / 8;
            int dataSize = samples * channels * bytesPerSample;
            
            using (var fileStream = new FileStream(filepath, FileMode.Create))
            using (var writer = new BinaryWriter(fileStream))
            {
                // WAV Header
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + dataSize); // File size - 8
                writer.Write("WAVE".ToCharArray());
                
                // Format chunk
                writer.Write("fmt ".ToCharArray());
                writer.Write(16); // Format chunk size
                writer.Write((short)1); // PCM format
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bytesPerSample); // Byte rate
                writer.Write((short)(channels * bytesPerSample)); // Block align
                writer.Write((short)bitDepth);
                
                // Data chunk
                writer.Write("data".ToCharArray());
                writer.Write(dataSize);
                
                // Audio data (convert float to 16-bit PCM)
                for (int i = 0; i < audioData.Length; i++)
                {
                    short sample = (short)(audioData[i] * 32767f);
                    writer.Write(sample);
                }
            }
        }
        
        /// <summary>
        /// Create export directory if it doesn't exist
        /// </summary>
        private void CreateExportDirectory()
        {
            string fullPath = Path.Combine(Application.dataPath, exportPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"📁 Created audio export directory: {fullPath}");
            }
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Generate audio for current animation
        /// </summary>
        [ContextMenu("Generate Audio for Current Animation")]
        public void EditorGenerateAudio()
        {
            GenerateAudioForCurrentAnimation();
        }
        
        /// <summary>
        /// Editor-only: Test audio generation with 5-second sample
        /// </summary>
        [ContextMenu("Generate 5-Second Test Audio")]
        public void EditorGenerateTestAudio()
        {
            if (animationSystem?.CurrentAnimation != null)
            {
                GenerateAudioForAnimation(animationSystem.CurrentAnimation, 5f);
            }
            else
            {
                Debug.LogWarning("❌ No animation selected for test audio generation");
            }
        }
        #endif
    }
}
