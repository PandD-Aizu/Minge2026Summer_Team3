using System;

namespace SaveSettings
{
    [Serializable]
    public class GameData
    {
        public AudioSettingsData audioSettings = new();
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