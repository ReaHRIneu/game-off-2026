using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameOff.EditorTools
{
    /// <summary>Web(WebGL) 빌드. 산출물은 저장소 루트 Builds/ (git 제외 경로)에 만든다.</summary>
    public static class Mvp1WebBuild
    {
        [MenuItem("GameOff/Build Web")]
        public static void Build()
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/Web"));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Mvp1Setup.ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Mvp1WebBuild] result=" + report.summary.result + " errors=" + report.summary.totalErrors + " size=" + report.summary.totalSize);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
