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
        /// <param name="masterVolume">全体音量</param>
        /// <param name="bgmVolume">BGM音量</param>
        /// <param name="seVolume">SE音量</param>
        /// <param name="environmentVolume">環境音量</param>
        /// <example>Presenterが現在のVCA音量を画面に反映する際に使用する</example>
        public void InitUIValue(float masterVolume, float bgmVolume, float seVolume, float environmentVolume)
        {
            // 表示の同期による音量変更イベントを発生させない
            masterVCA.SetValueWithoutNotify(masterVolume);
            bgmVCA.SetValueWithoutNotify(bgmVolume);
            seVCA.SetValueWithoutNotify(seVolume);
            environmentVCA.SetValueWithoutNotify(environmentVolume);
        }

        /// <summary>全スライダーの操作可否を切り替える</summary>
        /// <param name="interactable">音量の初期化が完了した場合にtrue</param>
        /// <example>Presenterの初期化時はfalse、接続完了時はtrueを指定する</example>
        public void SetInteractable(bool interactable)
        {
            masterVCA.interactable = interactable;
            bgmVCA.interactable = interactable;
            seVCA.interactable = interactable;
            environmentVCA.interactable = interactable;
        }
    }
}
