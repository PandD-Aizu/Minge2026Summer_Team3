using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>敵に捕まったことを短時間示す画面中央のパネル</summary>
public sealed class GameOverView : MonoBehaviour
{
    private Canvas _canvas;

    /// <summary>ゲーム画面の上に重ねるGameOverパネルを組み立てて隠す</summary>
    /// <example>FishingStageのView生成時にUnityが呼ぶ</example>
    private void Awake()
    {
        var canvasObject = new GameObject("Game Over Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 100;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        var backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        var backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0.09f, 0.025f, 0.035f, 0.76f);
        backdropImage.raycastTarget = false;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(backdrop.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 180f);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.13f, 0.96f);
        panelImage.raycastTarget = false;

        AddText(panel.transform, "Title", "GAME OVER", 56f, 14f);
        _canvas.enabled = false;
    }

    /// <summary>パネルの文字を中央に追加する</summary>
    /// <param name="parent">文字を置くパネル</param>
    /// <param name="objectName">Hierarchy上の名前</param>
    /// <param name="message">表示する文言</param>
    /// <param name="fontSize">最大の文字サイズ</param>
    /// <param name="padding">パネル端からの余白</param>
    /// <example>AddText(panel, "Title", "GAME OVER", 56f, 14f)で見出しを置く</example>
    private static void AddText(Transform parent, string objectName, string message, float fontSize, float padding)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);

        var label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = message;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMin = 28f;
        label.color = new Color(0.98f, 0.83f, 0.81f, 1f);
        label.raycastTarget = false;
    }

    /// <summary>敵との接触時にパネルを表示する</summary>
    /// <example>VisionEnemyControllerが入力を止めた後に呼ぶ</example>
    public void Show() => _canvas.enabled = true;

    /// <summary>復帰時またはシーン破棄時にパネルを閉じる</summary>
    /// <example>表示時間が終わったときに呼ぶ</example>
    public void Hide() => _canvas.enabled = false;
}
