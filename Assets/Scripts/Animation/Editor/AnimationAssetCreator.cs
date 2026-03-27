using UnityEngine;
using UnityEditor;
using BranchingLEDAnimator.Animation;

namespace BranchingLEDAnimator.Animation.Editor
{
    /// <summary>
    /// Editor utility to create LED animation assets
    /// </summary>
    public class AnimationAssetCreator
    {
        [MenuItem("Assets/Create/LED Animation/Wave Animation")]
        public static void CreateWaveAnimation()
        {
            CreateAnimationAsset<WaveAnimation>("WaveAnimation");
        }
        
        [MenuItem("Assets/Create/LED Animation/Pulse Animation")]
        public static void CreatePulseAnimation()
        {
            CreateAnimationAsset<PulseAnimation>("PulseAnimation");
        }
        
        [MenuItem("Assets/Create/LED Animation/Sparkle Animation")]
        public static void CreateSparkleAnimation()
        {
            CreateAnimationAsset<SparkleAnimation>("SparkleAnimation");
        }
        
        [MenuItem("Assets/Create/LED Animation/Breadth First Search Animation")]
        public static void CreateBFSAnimation()
        {
            CreateAnimationAsset<BreadthFirstSearchAnimation>("BreadthFirstSearchAnimation");
        }
        
        // Note: Progressive Path Animation menu item comes from CreateAssetMenu attribute on the class
        
        [MenuItem("Assets/Create/LED Animation/Create All Default Animations")]
        public static void CreateAllDefaultAnimations()
        {
            // Create Wave Animation
            var waveAnim = CreateAnimationAsset<WaveAnimation>("WaveAnimation");
            if (waveAnim != null)
            {
                waveAnim.animationName = "Wave";
                waveAnim.primaryColor = Color.red;
                waveAnim.secondaryColor = Color.yellow;
                waveAnim.speed = 1f;
                EditorUtility.SetDirty(waveAnim);
            }
            
            // Create Pulse Animation  
            var pulseAnim = CreateAnimationAsset<PulseAnimation>("PulseAnimation");
            if (pulseAnim != null)
            {
                pulseAnim.animationName = "Pulse";
                pulseAnim.primaryColor = Color.blue;
                pulseAnim.secondaryColor = Color.cyan;
                pulseAnim.speed = 1f;
                EditorUtility.SetDirty(pulseAnim);
            }
            
            // Create Sparkle Animation
            var sparkleAnim = CreateAnimationAsset<SparkleAnimation>("SparkleAnimation");
            if (sparkleAnim != null)
            {
                sparkleAnim.animationName = "Sparkle";
                sparkleAnim.primaryColor = Color.white;
                sparkleAnim.secondaryColor = Color.yellow;
                sparkleAnim.speed = 1f;
                EditorUtility.SetDirty(sparkleAnim);
            }
            
            // Create BFS Animation
            var bfsAnim = CreateAnimationAsset<BreadthFirstSearchAnimation>("BreadthFirstSearchAnimation");
            if (bfsAnim != null)
            {
                bfsAnim.animationName = "Breadth First Search";
                bfsAnim.primaryColor = Color.red;
                bfsAnim.secondaryColor = Color.blue;
                bfsAnim.speed = 0.5f; // Slower for better visibility
                bfsAnim.duration = 30f; // Longer duration for complex search
                bfsAnim.loop = true;
                EditorUtility.SetDirty(bfsAnim);
            }
            
            // Create Progressive Path Animation
            var pathAnim = CreateAnimationAsset<ProgressivePathAnimation>("ProgressivePathAnimation");
            if (pathAnim != null)
            {
                pathAnim.animationName = "Progressive Path";
                pathAnim.primaryColor = Color.cyan;
                pathAnim.secondaryColor = Color.magenta;
                pathAnim.speed = 1f;
                pathAnim.duration = 45f; // Long duration for multiple paths
                pathAnim.loop = true;
                EditorUtility.SetDirty(pathAnim);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("✓ Created all default LED animation assets!");
        }
        
        private static T CreateAnimationAsset<T>(string fileName) where T : LEDAnimationType
        {
            T asset = ScriptableObject.CreateInstance<T>();
            
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (path == "")
            {
                path = "Assets";
            }
            else if (System.IO.Path.GetExtension(path) != "")
            {
                path = path.Replace(System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
            }
            
            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + fileName + ".asset");
            
            AssetDatabase.CreateAsset(asset, assetPathAndName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            
            return asset;
        }
    }
}
