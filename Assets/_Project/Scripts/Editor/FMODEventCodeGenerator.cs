using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// FMODイベントのパスを取得し、FMODEventPathの定義スクリプトを生成するエディタ拡張
    /// </summary>
    [InitializeOnLoad]
    public class FMODEventCodeGenerator : EditorWindow
    {
        private const string OUTPUT_SCRIPT_PATH = "Assets/_Project/Scripts/FMODSettings/FMODEventPaths.cs";
        private const string NAMESPACE = "FMODSettings";
        private static DateTime lastCacheTime = DateTime.MinValue;
        private static double nextCheckTime;

        /// <summary>
        /// Unity起動時や再コンパイル後にバンク更新の監視を登録する
        /// </summary>
        static FMODEventCodeGenerator()
        {
            EditorApplication.update += GenerateAfterBankRefresh;
        }

        /// <summary>
        /// FMOD Studioでバンクをビルドした後に更新済みの一覧から生成する
        /// </summary>
        private static void GenerateAfterBankRefresh()
        {
            // 再生中やインポート中のスクリプト生成は保留し、終了後に最新の更新を処理する
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer
                || EditorApplication.timeSinceStartup < nextCheckTime)
            {
                return;
            }

            nextCheckTime = EditorApplication.timeSinceStartup + 1;

            // FMODの監視処理にバンクの読み直しを任せ、キャッシュが用意されるまで待つ
            var cacheTime = EventManager.CacheTime;
            if (cacheTime == DateTime.MinValue || cacheTime == lastCacheTime)
            {
                return;
            }

            lastCacheTime = cacheTime;
            try
            {
                Generate();
            }
            catch (Exception exception)
            {
                // 同じ更新で毎秒エラーを出さず、次のバンク更新か手動生成で再試行する
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Tools/FMOD/Generate Event Pathsまたはバンク更新監視から生成する
        /// </summary>
        [MenuItem("Tools/FMOD/Generate Event Paths")]
        private static void Generate()
        {
            // 読み込み失敗時に既存の定義を消さない 有効な空バンクなら空の定義を生成する
            if (!EventManager.IsValid)
            {
                Debug.LogWarning("[FMODEventCodeGenerator] FMOD event cache is not ready.");
                return;
            }

            var eventPaths = GetAllFMODEventPaths();
            if (!GenerateScript(eventPaths))
            {
                return;
            }

            AssetDatabase.ImportAsset(OUTPUT_SCRIPT_PATH);

            Debug.Log($"[FMODEventCodeGenerator] Generated {eventPaths.Count} events.");
        }

        /// <summary>
        /// FMODの更新済みキャッシュから取得したイベントパスを一定の順序で返す
        /// </summary>
        /// <returns>イベントパスのリスト</returns>
        private static List<string> GetAllFMODEventPaths()
        {
            var paths = new List<string>();

            foreach (var e in EventManager.Events)
            {
                paths.Add(e.Path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        /// <summary>
        /// イベントパス一覧からソースを生成し、内容が変わってファイルを保存した場合にtrueを返す
        /// </summary>
        /// <param name="eventPaths">イベントパスのリスト</param>
        /// <returns>内容が変わってファイルを保存した場合: true</returns>
        private static bool GenerateScript(List<string> eventPaths)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.");
            sb.AppendLine();
            sb.AppendLine("using FMODUnity;");
            sb.AppendLine();
            sb.AppendLine($"namespace {NAMESPACE}");
            sb.AppendLine("{");
            sb.AppendLine("    public readonly struct FMODEventPath");
            sb.AppendLine("    {");
            sb.AppendLine("        public EventReference Reference { get; }");
            sb.AppendLine("        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);");
            sb.AppendLine();

            foreach (var path in eventPaths)
            {
                var fieldName = PathToFieldName(path);
                sb.AppendLine($"        public static readonly FMODEventPath {fieldName} = new (\"{path}\");");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // 音声データだけの変更では書き換えず、再コンパイルと自動生成のループを防ぐ
            var source = sb.ToString();
            if (File.Exists(OUTPUT_SCRIPT_PATH)
                && File.ReadAllText(OUTPUT_SCRIPT_PATH).Replace("\r\n", "\n") == source.Replace("\r\n", "\n"))
            {
                return false;
            }

            // 出力ディレクトリがなければ作成する
            var dir = Path.GetDirectoryName(OUTPUT_SCRIPT_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            File.WriteAllText(OUTPUT_SCRIPT_PATH, source, Encoding.UTF8);
            return true;
        }

        /// <summary>
        /// event:/SE/WalkSandなどのパスを受け取り、SE_WALK_SANDのようなフィールド名を返す
        /// </summary>
        /// <param name="eventPath">イベントパス</param>
        /// <returns>フィールド名</returns>
        private static string PathToFieldName(string eventPath)
        {
            var withoutPrefix = eventPath.Replace("event:/", "");

            var snakeCase = System.Text.RegularExpressions.Regex.Replace(
                withoutPrefix,
                @"(?<=[a-z0-9])(?=[A-Z])",
                "_"
            );

            return snakeCase
                .Replace("/", "_")
                .Replace(" ", "_")
                .Replace("-", "_")
                .ToUpper();
        }
    }
}
