using TMPro;
using UnityEngine;
using System.Threading;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;

namespace _Project.Scripts.View
{
    public class MiniGameResultView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TextMeshProUGUI _resultText;
        [Header("釣り上げ演出")]
        [SerializeField] private TMP_FontAsset _fishFont;
        [SerializeField] private Sprite _messageBackground;
        [SerializeField, Min(0.1f)] private float _zoomSeconds = 0.7f;
        [SerializeField, Min(1f)] private float _fishDisplaySeconds = 5f;
        [SerializeField, Range(0.3f, 1f)] private float _cameraDistanceRatio = 0.65f;
        [SerializeField] private Vector3 _fishOffset = new(0f, 2.4f, 0f);

        private RectTransform _fishRoot;
        private RectTransform _fishHeader;
        private UnityEngine.UI.Image _fishImage;
        private TextMeshProUGUI _fishName;
        private TextMeshProUGUI _fishDescription;

        /// <summary>SOの釣果を頭上と説明欄へ表示し、カメラを寄せて元へ戻す</summary>
        /// <param name="fish">釣った魚の画像、名前、説明</param>
        /// <param name="player">頭上表示の基準となるプレイヤー</param>
        /// <param name="cancellation">シーン終了時に演出を中断するトークン</param>
        /// <returns>釣果表示とカメラ復帰が完了するまでの待機</returns>
        /// <example>魚の追加後にawait ShowFishAsync(fish, player, token)を呼ぶ</example>
        public async UniTask ShowFishAsync(FishDefinition fish, Transform player, CancellationToken cancellation)
        {
            if (fish == null || player == null) return;
            EnsureFishUI();

            // 釣り開始時に確定したSOを使い、次の魚の抽選には影響されないようにする
            _fishImage.sprite = fish.ItemImage;
            _fishImage.enabled = fish.ItemImage != null;
            _fishName.text = fish.ItemName;
            _fishDescription.text = fish.Description;
            _resultText.enabled = false;
            _canvas.enabled = true;
            _fishRoot.gameObject.SetActive(true);
            _fishHeader.gameObject.SetActive(false);

            var outputCamera = Camera.main;
            var brain = outputCamera != null ? outputCamera.GetComponent<CinemachineBrain>() : null;
            var activeCamera = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
            var composer = activeCamera != null ? activeCamera.GetComponent<CinemachinePositionComposer>() : null;
            float originalDistance = composer != null ? composer.CameraDistance : 0f;
            float closeDistance = originalDistance * _cameraDistanceRatio;
            float elapsed = 0f;
            float duration = _zoomSeconds * 2f + _fishDisplaySeconds;

            try
            {
                while (elapsed < duration)
                {
                    // Cinemachineの更新後に画面座標を求め、カメラ移動中も頭上へ追従する
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellation);
                    if (player == null || !isActiveAndEnabled) break;
                    if (brain != null && !ReferenceEquals(brain.ActiveVirtualCamera, activeCamera)) break;
                    elapsed += Time.deltaTime;

                    float zoom = elapsed < _zoomSeconds
                        ? Mathf.Clamp01(elapsed / _zoomSeconds)
                        : 1f - Mathf.Clamp01((elapsed - _zoomSeconds - _fishDisplaySeconds) / _zoomSeconds);
                    if (composer != null)
                        composer.CameraDistance = Mathf.Lerp(originalDistance, closeDistance, Mathf.SmoothStep(0f, 1f, zoom));

                    if (outputCamera != null)
                    {
                        var screen = outputCamera.WorldToScreenPoint(player.position + _fishOffset);
                        var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(_fishRoot, screen, uiCamera, out var local);
                        _fishHeader.anchoredPosition = local;
                        _fishHeader.gameObject.SetActive(screen.z > 0f);
                    }
                    else
                    {
                        _fishHeader.anchoredPosition = new Vector2(0f, 140f);
                        _fishHeader.gameObject.SetActive(true);
                    }
                }
            }
            finally
            {
                // シーン離脱や別カメラへの切替でもズームと釣果表示を残さない
                if (composer != null) composer.CameraDistance = originalDistance;
                if (_fishRoot != null) _fishRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>既存の結果Canvasと日本語フォントを使い、再利用する釣果UIを一度だけ作る</summary>
        /// <example>初回のShowFishAsyncから呼ぶ</example>
        private void EnsureFishUI()
        {
            if (_fishRoot != null) return;
            _fishRoot = CreateRect("CaughtFish", _canvas.transform, Vector2.zero, Vector2.zero);
            _fishRoot.anchorMin = Vector2.zero;
            _fishRoot.anchorMax = Vector2.one;
            _fishRoot.offsetMin = Vector2.zero;
            _fishRoot.offsetMax = Vector2.zero;

            // 頭上の画像と名前はひとまとまりとしてプレイヤーへ追従する
            _fishHeader = CreateRect("FishHeader", _fishRoot, new Vector2(620f, 230f), Vector2.zero);
            var imageRect = CreateRect("FishImage", _fishHeader, new Vector2(220f, 150f), new Vector2(0f, 40f));
            _fishImage = imageRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            _fishImage.preserveAspect = true;
            _fishImage.raycastTarget = false;
            var imageShadow = imageRect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            imageShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            imageShadow.effectDistance = new Vector2(4f, -4f);
            _fishName = CreateText("FishName", _fishHeader, new Vector2(620f, 70f), new Vector2(0f, -65f), 44f);
            ApplyFishNameStyle();

            // 説明は画面下の背景付きパネルに置き、海や魚影と重なっても読めるようにする
            var panel = CreateRect("FishDescriptionPanel", _fishRoot, Vector2.zero, Vector2.zero);
            panel.anchorMin = new Vector2(0.12f, 0.04f);
            panel.anchorMax = new Vector2(0.88f, 0.25f);
            var background = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = _messageBackground;
            background.color = new Color(1f, 0.9673854f, 0.89622635f, 1f);
            background.raycastTarget = false;
            _fishDescription = CreateText("FishDescription", panel, Vector2.zero, Vector2.zero, 32f);
            _fishDescription.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
            _fishDescription.rectTransform.anchorMin = Vector2.zero;
            _fishDescription.rectTransform.anchorMax = Vector2.one;
            _fishDescription.rectTransform.offsetMin = new Vector2(32f, 20f);
            _fishDescription.rectTransform.offsetMax = new Vector2(-32f, -20f);
        }

        /// <summary>魚名を白文字と濃い縁取り、影で強調し、明るい背景でも読めるようにする</summary>
        /// <example>魚名テキストの生成後にApplyFishNameStyle()を呼ぶ</example>
        private void ApplyFishNameStyle()
        {
            _fishName.color = Color.white;
            _fishName.fontSizeMin = 26f;

            // TMPが所有する個別マテリアルを使い、会話などの共有フォントには影響させない
            var material = _fishName.fontMaterial;
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.06f, 0.08f, 1f));
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.7f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
            _fishName.UpdateMeshPadding();
        }

        /// <summary>指定した親の中央に表示用のRectTransformを作る</summary>
        /// <param name="objectName">生成するオブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="size">表示サイズ</param>
        /// <param name="position">中央からの位置</param>
        /// <returns>生成したRectTransform</returns>
        /// <example>CreateRect("FishImage", parent, new Vector2(220, 150), Vector2.zero)</example>
        private static RectTransform CreateRect(string objectName, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        /// <summary>会話用の日本語フォントで可変サイズのテキストを作る</summary>
        /// <param name="objectName">生成するオブジェクト名</param>
        /// <param name="parent">配置先</param>
        /// <param name="size">表示サイズ</param>
        /// <param name="position">中央からの位置</param>
        /// <param name="fontSize">文字サイズの上限</param>
        /// <returns>生成したテキスト</returns>
        /// <example>CreateText("FishName", parent, new Vector2(620, 65), Vector2.zero, 38)</example>
        private TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 size, Vector2 position, float fontSize)
        {
            var text = CreateRect(objectName, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _fishFont;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = fontSize;
            text.richText = false;
            text.raycastTarget = false;
            return text;
        }

        void Start()
        {
            _canvas.enabled = false;
        }

        public void GreatResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Great!";
        }

        public void GoodResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Good!";
        }

        public void MissResult()
        {
            BringToFront();
            _canvas.enabled = true;
            _resultText.enabled = true;
            _resultText.text = "Miss!";
        }

        public void HideResult()
        {
            _resultText.enabled = false;
        }

        private void BringToFront()
        {
            transform.SetAsLastSibling();
            _resultText.transform.SetAsLastSibling();
        }
    }


}
