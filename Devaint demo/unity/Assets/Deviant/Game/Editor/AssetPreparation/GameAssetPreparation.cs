using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Deviant.Game.Editor
{
    public static class GameAssetPreparation
    {
        public static bool Prepare(string entryName)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-u tools/assets/prepare_game_sandbox.py",
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            EditorUtility.DisplayProgressBar(entryName, "고정 리소스를 확인하고 Unity 에셋을 준비하는 중", 0.5f);
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                using var process = Process.Start(start);
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException(errors.GetAwaiter().GetResult());
                UnityEngine.Debug.Log(output.GetAwaiter().GetResult());
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogError(error);
                EditorUtility.DisplayDialog(entryName + " 실행 준비 실패",
                    "Python, Git LFS, GitLab 리소스 접근 권한과 resources의 로컬 변경을 확인하세요. 자세한 이유는 Console에 있습니다.", "확인");
                return false;
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return true;
        }
    }
}
