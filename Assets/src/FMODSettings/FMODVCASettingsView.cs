using R3;
using UnityEngine;
using UnityEngine.UI;

namespace FMODSettings
{
    public class FMODVCASettingsView : MonoBehaviour
    {
        [SerializeField] public Slider masterVCA;
        [SerializeField] public Slider bgmVCA;
        [SerializeField] public Slider seVCA;
        [SerializeField] public Slider environmentVCA;
        
        public Observable<float> MasterVCA => masterVCA.OnValueChangedAsObservable();
        public Observable<float> BgmVCA => bgmVCA.OnValueChangedAsObservable();
        public Observable<float> SeVCA => seVCA.OnValueChangedAsObservable();
        public Observable<float> EnvironmentVCA => environmentVCA.OnValueChangedAsObservable();
        
        /// <summary>
        /// UIの値を初期化する
        /// </summary>
        public void InitUIValue(float masterVolume, float bgmVolume, float seVolume, float environmentVolume)
        {
            masterVCA.value = masterVolume;
            bgmVCA.value = bgmVolume;
            seVCA.value = seVolume;
            environmentVCA.value = environmentVolume;
        }
    }
}