using System;

namespace SaveSettings
{
    [Serializable]
    public class GameData
    {
        public bool hasEconomy;
        public int balance;
        public System.Collections.Generic.List<OwnedItemData> ownedItems = new();
        public AudioSettingsData audioSettings = new();
        // 会話履歴だけを初回保存した場合は、FMOD側の既定音量を維持する
        public bool useDefaultAudioSettings;
        public System.Collections.Generic.List<string> shownDialogueIds = new();
    }

    [Serializable]
    public class OwnedItemData
    {
        public int itemId;
        public int count;
    }

    [Serializable]
    public class AudioSettingsData
    {
        public float masterVolume;
        public float bgmVolume;
        public float seVolume;
        public float environmentVolume;
    }
}
