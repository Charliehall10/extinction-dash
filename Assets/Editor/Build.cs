using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sets the project up from scratch and builds it. The game creates all of its
// objects at runtime, so the scene is empty.
//
//   Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Build.WebGL
//   Unity -batchmode -quit -projectPath . -buildTarget Linux64 -executeMethod Build.Linux
public static class Build
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Extinction Dash/Prepare Project")]
    public static void Prepare()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "Charlie Hall";
        PlayerSettings.productName = "Extinction Dash";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.runInBackground = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        PlayerSettings.WebGL.template = "PROJECT:ExtinctionDash";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Extinction Dash/Build WebGL")]
    public static void WebGL()
    {
        Prepare();
        Run(BuildTarget.WebGL, "Builds/WebGL");
    }

    [MenuItem("Extinction Dash/Build Linux")]
    public static void Linux()
    {
        Prepare();
        Run(BuildTarget.StandaloneLinux64, "Builds/Linux/ExtinctionDash.x86_64");
    }

    static void Run(BuildTarget target, string path)
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = target,
            options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"[Build] {target}: {s.result}, {s.totalErrors} errors, {s.totalSize / 1024} KB, {s.totalTime.TotalSeconds:0}s -> {path}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
