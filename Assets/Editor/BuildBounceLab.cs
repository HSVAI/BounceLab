using System.IO;
using BounceLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BounceLab.Editor
{
    public static class BuildBounceLab
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void BuildAndroid()
        {
            VerifyRules();
            EnsureScene();
            ConfigureCommon();
            PlayerSettings.productName = "Bounce Lab";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.ghtnql.bouncelab");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
            PlayerSettings.Android.bundleVersionCode = 4;
            // First playable is a local-test APK, not a store release.
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = "/opt/Unity/2022.3.62f3/Data/PlaybackEngines/AndroidPlayer/SDK";
            UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = "/opt/Unity/2022.3.62f3/Data/PlaybackEngines/AndroidPlayer/NDK";
            UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = "/opt/Unity/2022.3.62f3/Data/PlaybackEngines/AndroidPlayer/OpenJDK";
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Android/BounceLab.apk", BuildTarget.Android, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Android build failed: " + report.summary.result);
            Debug.Log("BOUNCELAB_ANDROID_BUILD_OK bytes=" + report.summary.totalSize);
        }

        public static void BuildLinux()
        {
            VerifyRules();
            EnsureScene();
            ConfigureCommon();
            // Headless QA has no window manager to focus the player window.
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            Directory.CreateDirectory("Builds/Linux");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Linux/BounceLab.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Linux build failed: " + report.summary.result);
        }

        public static void BuildWebGL()
        {
            VerifyRules();
            EnsureScene();
            ConfigureCommon();
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            Directory.CreateDirectory("Builds/WebGL");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("WebGL build failed: " + report.summary.result);
            TuneWebGLTemplate("Builds/WebGL", "Bounce Lab — Web Play", "#050a12");
            Debug.Log("BOUNCELAB_WEBGL_BUILD_OK bytes=" + report.summary.totalSize);
        }

        private static void TuneWebGLTemplate(string outputPath, string title, string background)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            string html = File.ReadAllText(indexPath)
                .Replace("<html lang=\"en-us\">", "<html lang=\"ko\">")
                .Replace("<title>Unity WebGL Player | Bounce Lab</title>", "<title>" + title + "</title>")
                .Replace("// config.autoSyncPersistentDataPath = true;", "config.autoSyncPersistentDataPath = true;")
                .Replace("// config.devicePixelRatio = 1;", "config.devicePixelRatio = 1;");
            File.WriteAllText(indexPath, html);

            string stylePath = Path.Combine(outputPath, "TemplateData/style.css");
            string css = File.ReadAllText(stylePath);
            const string marker = "/* RESPONSIVE_PORTRAIT */";
            if (!css.Contains(marker))
            {
                css += "\n" + marker + "\nhtml,body{width:100%;height:100%;overflow:hidden;background:" + background + ";}" +
                    "#unity-container,#unity-container.unity-desktop,#unity-container.unity-mobile{position:fixed;inset:0;left:0;top:0;transform:none;width:min(100vw,56.25vh);height:min(100vh,177.7778vw);margin:auto;}" +
                    "#unity-canvas,#unity-canvas.unity-mobile{display:block;width:100%!important;height:100%!important;}#unity-footer{display:none;}\n";
                File.WriteAllText(stylePath, css);
            }
        }

        private static void ConfigureCommon()
        {
            PlayerSettings.companyName = "ghtnql";
            PlayerSettings.productName = "Bounce Lab";
            PlayerSettings.bundleVersion = "0.3.1";
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }

        public static void VerifyRules()
        {
            var blank = MapRules.Blank();
            Assert(MapRules.Validate(blank) == "", "Blank editor map is valid");
            Assert(blank.tiles.Length == MapRules.Width * MapRules.Height, "Fixed map dimensions");
            Assert(MapRules.Tile(blank, -1, 2) == MapRules.Block, "Outside map is a wall");
            var missingStart = blank.Clone();
            missingStart.tiles[MapRules.Index(2, 1)] = MapRules.Empty;
            Assert(MapRules.Validate(missingStart) == "PLACE EXACTLY ONE START", "Start required");
            var duplicateGoal = blank.Clone();
            duplicateGoal.tiles[MapRules.Index(5, 5)] = MapRules.Goal;
            Assert(MapRules.Validate(duplicateGoal) == "PLACE EXACTLY ONE GOAL", "Single goal required");
            var invalidTile = blank.Clone();
            invalidTile.tiles[3] = 99;
            Assert(MapRules.Validate(invalidTile) == "UNKNOWN TILE", "Tile range validation");
            Assert(MapRules.Validate(MapRules.Training()) == "", "Training map is valid");
            Assert(MapRules.IsSolid(MapRules.Block) && MapRules.IsSolid(MapRules.Spring), "Solid tiles");
            Assert(!MapRules.IsSolid(MapRules.Spike), "Spike is a trigger");
            string audioMetrics = BounceAudio.CompositionMetrics();
            string[] audio = audioMetrics.Split(',');
            Assert(audio.Length == 4 && int.Parse(audio[0]) > 300000, "Music loop duration");
            Assert(float.Parse(audio[1], System.Globalization.CultureInfo.InvariantCulture) > .15f &&
                float.Parse(audio[1], System.Globalization.CultureInfo.InvariantCulture) <= .8f, "Music peak range");
            Assert(float.Parse(audio[2], System.Globalization.CultureInfo.InvariantCulture) > .025f && int.Parse(audio[3]) > 1000,
                "Music energy and note activity");
            Debug.Log("BOUNCELAB_AUDIO_METRICS samples,peak,rms,crossings=" + audioMetrics);
            Debug.Log("BOUNCELAB_RULE_TESTS_PASS assertions=12");
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new System.Exception("Rule test failed: " + name);
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Bounce Lab");
            game.AddComponent<BounceLabGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
    }
}
