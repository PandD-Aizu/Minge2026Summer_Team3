using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 文字を表示するために必須の宣言

public class InventoryView : MonoBehaviour
{
    // テンプレート通りのイベント（拡声器）
    public event Action<int> OnItemSelected;
    public event Action OnUseClicked;
    public event Action<ItemType> OnTabSelected;
    public event Action OnCloseClicked;

    // --- ここからインスペクターで紐付けるための枠 ---
    [Header("タブボタン")]
    [SerializeField] private Button rodButton;
    [SerializeField] private Button ingredientButton;
    [SerializeField] private Button fishButton;

    [Header("切り替えるスクロールビュー")]
    [SerializeField] private GameObject scrollViewRod;
    [SerializeField] private GameObject scrollViewIngredient;
    [SerializeField] private GameObject scrollViewFish;

    [Header("閉じるボタン")]
    [SerializeField] private Button closeButton;

    [Header("詳しく出す説明(DetailView)のパーツ")]
    [SerializeField] private GameObject detailView; // マスの下のはみ出し部屋(Empty)
    [SerializeField] private Image detailIcon;        // イラスト
    [SerializeField] private TextMeshProUGUI detailNameText; // 名前（あれば）
    [SerializeField] private TextMeshProUGUI detailDescriptionText; // 「よさ」などの説明
    [SerializeField] private TextMeshProUGUI detailCountText;       // 個数（下に出す場合用）
    [SerializeField] private Button useButton;        // 「使う」ボタン（[F]キー連動用など）

    // プロジェクト内にItemTypeがない場合のエラーを防ぐための仮定義（すでにある場合は削除してください）
    public enum ItemType { Rod, Ingredient, Fish }

    private void Start()
    {
        // 1. 各タブボタンが押された時の処理
        if (rodButton != null) rodButton.onClick.AddListener(() => { SwitchTab(0); OnTabSelected?.Invoke(ItemType.Rod); });
        if (ingredientButton != null) ingredientButton.onClick.AddListener(() => { SwitchTab(1); OnTabSelected?.Invoke(ItemType.Ingredient); });
        if (fishButton != null) fishButton.onClick.AddListener(() => { SwitchTab(2); OnTabSelected?.Invoke(ItemType.Fish); });

        // 2. 使うボタン・閉じるボタンが押された時の処理
        if (useButton != null) useButton.onClick.AddListener(() => OnUseClicked?.Invoke());
        if (closeButton != null) closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());

        // 起動時は、詳細画面（DetailView）を隠しておく
        if (detailView != null) detailView.SetActive(false);

        // 初期状態：最初のタブ（釣り具）を表示
        SwitchTab(0);
    }

    // タブのスクロールビューを一括で切り替える内部関数
    private void SwitchTab(int tabIndex)
    {
        if (scrollViewRod != null) scrollViewRod.SetActive(false);
        if (scrollViewIngredient != null) scrollViewIngredient.SetActive(false);
        if (scrollViewFish != null) scrollViewFish.SetActive(false);

        switch (tabIndex)
        {
            case 0: if (scrollViewRod != null) scrollViewRod.SetActive(true); break;
            case 1: if (scrollViewIngredient != null) scrollViewIngredient.SetActive(true); break;
            case 2: if (scrollViewFish != null) scrollViewFish.SetActive(true); break;
        }
    }

    // --- ここからテンプレートの関数の中身を実装 ---

    // インベントリ画面全体を表示する
    public void Show() 
    {
        gameObject.SetActive(true);
    }

    // インベントリ画面全体を非表示にする
    public void Hide() 
    {
        gameObject.SetActive(false);
    }

    // アイテムが選択されたときに、そのアイテムのデータをUIに流し込む（セットする）
    public void SetSelectedItem(Sprite icon, string itemName, string description, int count)
    {
        // 1. 非表示になっていた説明部屋(DetailView)を表示する
        if (detailView != null) detailView.SetActive(true);

        // 2. 各UIパーツにアイテムの情報を流し込む
        if (detailIcon != null) detailIcon.sprite = icon;
        if (detailDescriptionText != null) detailDescriptionText.text = description; // 「よさ：〇〇」などを代入
        if (detailCountText != null) detailCountText.text = count.ToString();
        if (detailNameText != null) detailNameText.text = itemName;
    }

    // 「使う」ボタンのポチポチ（有効・無効）を切り替える
    public void SetUseButtonInteractable(bool interactable)
    {
        if (useButton != null)
        {
            useButton.interactable = interactable;
        }
    }
}
