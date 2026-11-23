using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

public class AndroidBuildPipeline : EditorWindow
{
    private const string PREF_BUILD_AAB = "AAGames_AndroidBuilder_AAB";
    private const string PREF_AUTO_INC = "AAGames_AndroidBuilder_AutoInc";
    private const string PREF_IS_DEV = "AAGames_AndroidBuilder_IsDev";
    private const string PREF_OPEN_FOLDER = "AAGames_AndroidBuilder_OpenFolder";
    private const string PREF_OUTPUT_PATH = "AAGames_AndroidBuilder_OutputPath";

    private bool buildAppBundle;
    private bool autoIncrementVersionCode;
    private bool isDevelopmentBuild;
    private bool openFolderAfterBuild;
    private string customOutputFolder;

    // TODO: Add ?aid=YOUR_AFFILIATE_ID only after acceptance into the Unity Affiliate Program
    private const string AFFILIATE_URL = "https://assetstore.unity.com/packages/tools/utilities/build-report-inspector-161257";

    [MenuItem("Tools/Android Build Automator")]
    public static void ShowWindow()
    {
        GetWindow<AndroidBuildPipeline>("Android Builder");
    }

    private void OnEnable()
    {
        buildAppBundle = EditorPrefs.GetBool(PREF_BUILD_AAB, false);
        autoIncrementVersionCode = EditorPrefs.GetBool(PREF_AUTO_INC, true);
        isDevelopmentBuild = EditorPrefs.GetBool(PREF_IS_DEV, false);
        openFolderAfterBuild = EditorPrefs.GetBool(PREF_OPEN_FOLDER, true);
        customOutputFolder = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Builds/Android");
    }

    private void OnGUI()
    {
        // Keep long labels (e.g. "Auto-Increment Version Code") from being cut off in narrow windows
        EditorGUIUtility.labelWidth = 220f;

        GUILayout.Space(10);
        GUILayout.Label("Android Quick Build Pipeline", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Automates version bumping, clean output paths, and safe builds.", MessageType.Info);

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Current Project Settings", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Package Name:", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        EditorGUILayout.LabelField("Version Name:", PlayerSettings.bundleVersion);
        EditorGUILayout.LabelField("Bundle Version Code:", PlayerSettings.Android.bundleVersionCode.ToString());

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Build Configuration", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        buildAppBundle = EditorGUILayout.Toggle("Build AAB (Google Play)", buildAppBundle);
        autoIncrementVersionCode = EditorGUILayout.Toggle("Auto-Increment Version Code", autoIncrementVersionCode);
        
        if (!buildAppBundle)
        {
            isDevelopmentBuild = EditorGUILayout.Toggle("Development Build (APK)", isDevelopmentBuild);
        }
        else
        {
            if (isDevelopmentBuild)
            {
                isDevelopmentBuild = false;
                EditorPrefs.SetBool(PREF_IS_DEV, false);
            }
        }

        openFolderAfterBuild = EditorGUILayout.Toggle("Open Folder When Done", openFolderAfterBuild);
        customOutputFolder = EditorGUILayout.TextField("Output Directory", customOutputFolder);

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(PREF_BUILD_AAB, buildAppBundle);
            EditorPrefs.SetBool(PREF_AUTO_INC, autoIncrementVersionCode);
            EditorPrefs.SetBool(PREF_IS_DEV, isDevelopmentBuild);
            EditorPrefs.SetBool(PREF_OPEN_FOLDER, openFolderAfterBuild);
            EditorPrefs.SetString(PREF_OUTPUT_PATH, customOutputFolder);
        }

        GUILayout.Space(15);

        bool isBusy = EditorApplication.isCompiling || EditorApplication.isPlaying;
        if (isBusy)
        {
            EditorGUILayout.HelpBox("Editor is compiling or in Play mode. Please wait...", MessageType.Warning);
        }

        GUI.enabled = !isBusy;
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
        string buttonLabel = buildAppBundle ? "Build Release (.AAB)" : (isDevelopmentBuild ? "Build Development (.APK)" : "Build Release (.APK)");
        if (GUILayout.Button(buttonLabel, GUILayout.Height(40)))
        {
            RunAndroidBuild();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Need deeper build size tracking?", EditorStyles.miniBoldLabel);
        GUILayout.Label("Check out Build Report Inspector on the Unity Asset Store:", EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("View Recommended Tool (Asset Store)", EditorStyles.miniButton))
        {
            Application.OpenURL(AFFILIATE_URL);
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void RunAndroidBuild()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Build Error", "No enabled scenes found in Build Settings!", "OK");
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (EditorUtility.DisplayDialog("Switch Platform", "Active build target is not Android. Switch now?", "Yes", "No"))
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            return;
        }

        EditorUserBuildSettings.buildAppBundle = buildAppBundle;

        string fullOutputDir;
        try
        {
            fullOutputDir = Path.GetFullPath(string.IsNullOrEmpty(customOutputFolder) ? "Builds/Android" : customOutputFolder);
            if (!Directory.Exists(fullOutputDir))
            {
                Directory.CreateDirectory(fullOutputDir);
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Build Error", $"Invalid output directory path:\n{ex.Message}", "OK");
            return;
        }

        string rawProductName = Application.productName;
        string cleanName = new string(rawProductName.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray())
            .Replace(" ", "_");
        if (string.IsNullOrEmpty(cleanName)) cleanName = "GameBuild";

        string extension = buildAppBundle ? "aab" : "apk";
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string fileName = $"{cleanName}_v{PlayerSettings.bundleVersion}_b{PlayerSettings.Android.bundleVersionCode}_{timestamp}.{extension}";
        string fullPath = Path.Combine(fullOutputDir, fileName);

        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullPath,
            target = BuildTarget.Android,
            options = isDevelopmentBuild ? BuildOptions.Development : BuildOptions.None
        };

        Debug.Log($"[AndroidBuilder] Starting build: {fullPath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            if (autoIncrementVersionCode)
            {
                PlayerSettings.Android.bundleVersionCode += 1;
                Debug.Log($"[AndroidBuilder] Build succeeded! Incremented Bundle Version Code to: {PlayerSettings.Android.bundleVersionCode}");
            }

            float sizeMb = summary.totalSize / (1024f * 1024f);
            Debug.Log($"[AndroidBuilder] Build Succeeded! Size: {sizeMb:F1} MB");

            if (openFolderAfterBuild)
            {
                EditorUtility.RevealInFinder(fullPath);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Build Failed", $"Android build failed with result: {summary.result}", "OK");
            Debug.LogError($"[AndroidBuilder] Build Failed: {summary.result}");
        }
    }
}