using FMOD.Studio;
using FMODUnity;

namespace FMODServices
{
    /// <summary>
    /// FMODのVCA（ボリュームコントロール）を管理するサービスクラス
    /// </summary>
    public class FMODVCAService
    {
        private VCA masterVCA;
        private VCA bgmVCA;
        private VCA seVCA;
        private VCA environmentVCA;
        
        /// <summary>
        /// VCAをRuntimeManagerから取得してリンクする
        /// </summary>
        public void LinkVCAs()
        {
            masterVCA = RuntimeManager.GetVCA("vca:/Master");
            bgmVCA = RuntimeManager.GetVCA("vca:/BGM");
            seVCA = RuntimeManager.GetVCA("vca:/SE");
            environmentVCA = RuntimeManager.GetVCA("vca:/Environment");
        }

        public void InitVCAs(float masterVolume = 1.0f, float bgmVolume = 1.0f, float seVolume = 1.0f, float environmentVolume = 1.0f)
        {
            masterVCA.setVolume(masterVolume);
            bgmVCA.setVolume(bgmVolume);
            seVCA.setVolume(seVolume);
            environmentVCA.setVolume(environmentVolume);
        }
        
        public void SetMasterVolume(float volume)
        {
            masterVCA.setVolume(volume);
        }

        public void SetBgmVolume(float volume)
        {
            bgmVCA.setVolume(volume);
        }

        public void SetSeVolume(float volume)
        {
            seVCA.setVolume(volume);
        }

        public void SetEnvironmentVolume(float volume)
        {
            environmentVCA.setVolume(volume);
        }
        
        public float GetMasterVolume()
        {
            masterVCA.getVolume(out float volume);
            return volume;
        }
        
        public float GetBgmVolume()
        {
            bgmVCA.getVolume(out float volume);
            return volume;
        }
        
        public float GetSeVolume()
        {
            seVCA.getVolume(out float volume);
            return volume;
        }

        public float GetEnvironmentVolume()
        {
            environmentVCA.getVolume(out float volume);
            return volume;
        }
    }
}