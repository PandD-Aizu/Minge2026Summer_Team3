using System;
using System.IO;
using System.Reflection.Metadata;
using UnityEngine;

namespace SaveSettings
{
    public class SaveService
    {
        private const string SaveFileName = "GameData.json";
        private readonly string _savePath = Path.Combine(UnityEngine.Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// セーブデータを保存する
        /// </summary>
        /// <param name="data">保存するデータ</param>
        public void WriteSaveData(GameData data)
        {
            if (data is null)
                return;
            
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save data: {ex.Message}");
                Debug.LogException(ex);
            }
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