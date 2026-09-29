using System;
using System.IO;
using VContainer;
using UnityEngine;

namespace SaveSettings
{
    public class SaveService
    {
        private const string SaveFileName = "GameData.json";
        private readonly string _savePath;

        /// <summary>通常のゲーム用セーブ先を使用する</summary>
        /// <example>VContainerがRoot Scopeで生成する</example>
        [Inject]
        public SaveService() : this(Path.Combine(Application.persistentDataPath, SaveFileName)) { }

        /// <summary>指定したファイルをセーブ先として使用する</summary>
        /// <param name="savePath">検証などに使用する保存ファイルのパス</param>
        /// <example>テストでは一時フォルダー内のファイルを指定する</example>
        public SaveService(string savePath) => _savePath = savePath;

        /// <summary>
        /// セーブデータを保存する
        /// </summary>
        /// <param name="data">保存するデータ</param>
        /// <returns>保存できた場合はtrue</returns>
        public bool WriteSaveData(GameData data)
        {
            if (data is null)
                return false;
            
            try
            {
                var saveDirectory = Path.GetDirectoryName(_savePath);
                if (!string.IsNullOrEmpty(saveDirectory))
                {
                    Directory.CreateDirectory(saveDirectory);
                }

                var json = JsonUtility.ToJson(data, true);
                var temporaryPath = $"{_savePath}.tmp";
                File.WriteAllText(temporaryPath, json);

                if (File.Exists(_savePath))
                {
                    File.Replace(temporaryPath, _savePath, null);
                }
                else
                {
                    File.Move(temporaryPath, _savePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save data: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>現在のセーブに会話の表示履歴があるか確認する</summary>
        /// <param name="dialogueId">会話アセット固有の保存ID</param>
        /// <returns>表示済みならtrue、旧形式や未保存ならfalse</returns>
        /// <example>会話開始前にHasShownDialogue(data.SaveId)を呼ぶ</example>
        public bool HasShownDialogue(string dialogueId)
        {
            return !string.IsNullOrEmpty(dialogueId)
                && LoadSaveData()?.shownDialogueIds?.Contains(dialogueId) == true;
        }

        /// <summary>ほかの保存項目を維持して会話の表示履歴を保存する</summary>
        /// <param name="dialogueId">表示が始まった会話の保存ID</param>
        /// <returns>保存済みまたは保存に成功した場合はtrue</returns>
        /// <example>会話の開始成功後にMarkDialogueShown(data.SaveId)を呼ぶ</example>
        public bool MarkDialogueShown(string dialogueId)
        {
            if (string.IsNullOrEmpty(dialogueId)) return false;
            var data = LoadSaveData();

            // 読めない既存ファイルを初期データで上書きしない
            if (data == null && File.Exists(_savePath)) return false;

            // 会話だけの初回保存で、未設定の音量をゼロとして保存しない
            data ??= new GameData { useDefaultAudioSettings = true };
            data.shownDialogueIds ??= new System.Collections.Generic.List<string>();
            if (data.shownDialogueIds.Contains(dialogueId)) return true;
            data.shownDialogueIds.Add(dialogueId);
            return WriteSaveData(data);
        }

        /// <summary>
        /// セーブデータを読み込む
        /// </summary>
        /// <returns>読み込んだセーブデータ</returns>
        public GameData LoadSaveData()
        {
            if (!File.Exists(_savePath))
                return null;

            try
            {
                var json = File.ReadAllText(_savePath);
                return JsonUtility.FromJson<GameData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load data: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }

        /// <summary>
        /// セーブデータを削除する
        /// </summary>
        public void DeleteSaveData()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    File.Delete(_savePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete save data: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}
