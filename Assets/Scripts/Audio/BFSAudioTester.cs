using UnityEngine;
using BranchingLEDAnimator.Audio;
using BranchingLEDAnimator.Core;
using BranchingLEDAnimator.Animation;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BranchingLEDAnimator.Audio
{
    /// <summary>
    /// Simple component to test BFS audio generation with different settings
    /// </summary>
    public class BFSAudioTester : MonoBehaviour
    {
        [Header("Quick BFS Audio Test")]
        [SerializeField] private BFSAudioGenerator.MusicalScale testScale = BFSAudioGenerator.MusicalScale.Pentatonic;
        [SerializeField] private float testDuration = 10f;
        [SerializeField] private bool enableAmbientDrone = true;
        [SerializeField] private bool enableHarmonicChimes = true;
        
        private LEDAudioGenerator audioGenerator;
        private LEDAnimationSystem animationSystem;
        
        void Start()
        {
            audioGenerator = GetComponent<LEDAudioGenerator>();
            animationSystem = GetComponent<LEDAnimationSystem>();
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Test BFS Audio - Pentatonic Scale")]
        public void TestPentatonicBFSAudio()
        {
            testScale = BFSAudioGenerator.MusicalScale.Pentatonic;
            GenerateBFSTestAudio();
        }
        
        [ContextMenu("Test BFS Audio - Dorian Mode")]
        public void TestDorianBFSAudio()
        {
            testScale = BFSAudioGenerator.MusicalScale.Dorian;
            GenerateBFSTestAudio();
        }
        
        [ContextMenu("Test BFS Audio - Lydian Mode")]
        public void TestLydianBFSAudio()
        {
            testScale = BFSAudioGenerator.MusicalScale.Lydian;
            GenerateBFSTestAudio();
        }
        
        [ContextMenu("Generate BFS Audio for Current Animation")]
        public void GenerateBFSTestAudio()
        {
            if (audioGenerator == null)
            {
                audioGenerator = GetComponent<LEDAudioGenerator>();
            }
            
            if (animationSystem == null)
            {
                animationSystem = GetComponent<LEDAnimationSystem>();
            }
            
            if (audioGenerator == null)
            {
                Debug.LogError("❌ LEDAudioGenerator component not found!");
                return;
            }
            
            if (animationSystem?.CurrentAnimation == null)
            {
                Debug.LogWarning("❌ No animation selected. Please select a BreadthFirstSearchAnimation.");
                return;
            }
            
            // Check if it's a BFS animation
            if (!(animationSystem.CurrentAnimation is BreadthFirstSearchAnimation))
            {
                Debug.LogWarning("⚠️ Current animation is not a BreadthFirstSearchAnimation. Results may vary.");
            }
            
            // Configure for BFS specialized mode
            var generationModeField = typeof(LEDAudioGenerator).GetField("generationMode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (generationModeField != null)
            {
                generationModeField.SetValue(audioGenerator, LEDAudioGenerator.AudioGenerationMode.BFS_Specialized);
                Debug.Log("🎵 Set audio generation mode to BFS_Specialized");
            }
            
            // Generate the audio
            Debug.Log($"🎵 Generating BFS audio with {testScale} scale for {testDuration} seconds");
            audioGenerator.GenerateAudioForAnimation(animationSystem.CurrentAnimation, testDuration);
        }
        
        [ContextMenu("Show BFS Audio Help")]
        public void ShowBFSAudioHelp()
        {
            string helpMessage = @"🎵 BFS Audio System Help

WORKFLOW:
1. Select a BreadthFirstSearchAnimation in LEDAnimationSystem
2. Set LEDAudioGenerator mode to 'BFS_Specialized'
3. Use the context menu options to test different scales
4. Generated audio will be saved to Assets/AudioExports/

MUSICAL SCALES:
• Pentatonic: Very harmonious, ambient (C D E G A)
• Dorian: Modal, mysterious (C D Eb F G A Bb)  
• Lydian: Dreamy, floating (C D E F# G A B)
• Minor: Emotional, darker (C D Eb F G Ab Bb)
• Major: Bright, happy (C D E F G A B)

AUDIO FEATURES:
• Ambient drone layer for atmosphere
• Harmonic chimes triggered by graph topology
• Node valence mapped to pitch/harmony
• Search depth mapped to reverb/space
• Exponential decay envelopes for natural sound

TIPS:
• Lower frequencies (220Hz root) = more ambient
• Higher frequencies (440Hz root) = more melodic
• Enable harmonic chimes for richer texture
• Adjust chime decay for longer/shorter tails";

            EditorUtility.DisplayDialog("BFS Audio System Help", helpMessage, "Got it!");
        }
        #endif
    }
}
