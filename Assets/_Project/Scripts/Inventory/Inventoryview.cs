using R3;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 文字を表示するために必須の宣言
using ItemMenus;

public class InventoryView : MonoBehaviour
{
    [SerializeField] private ItemMenuNavigation _navigation;
    public ItemMenuNavigation Navigation => _navigation;
    private readonly Subject<int> _itemSelected = new();
    private readonly Subject<Unit> _useClicked = new();
    private readonly Subject<ItemType> _tabSelected = new();
    private readonly Subject<Unit> _closeClicked = new();

    public Observable<int> OnItemSelected => _itemSelected;
    public Observable<Unit> OnUseClicked => _useClicked;
    public Observable<ItemType> OnTabSelected => _tabSelected;
    public Observable<Unit> OnCloseClicked => _closeClicked;

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
    [SerializeField] private Button useButton;        // 「使う」ボタン（Jキーと同じ使用処理を呼ぶ）

    // プロジェクト内にItemTypeがない場合のエラーを防ぐための仮定義（すでにある場合は削除してください）
    public enum ItemType { Rod, Ingredient, Fish }

    /// <summary>タブと使用・閉じるボタンの操作を接続する</summary>
    /// <example>Prefabの生成時にUnityが呼ぶ</example>
    private void Awake()
    {
        // 1. 各タブボタンが押された時の処理
        if (_navigation != null)
        {
            _navigation.OnTabSelected.Subscribe(HandleTabSelected).AddTo(this);
        }
        else
        {
            // 既存のUI確認用シーンでは従来のボタン参照を利用する
            if (rodButton != null) rodButton.OnClickAsObservable()
                .Subscribe(_ => { SwitchTab(0); _tabSelected.OnNext(ItemType.Rod); }).AddTo(this);
            if (ingredientButton != null) ingredientButton.OnClickAsObservable()
                .Subscribe(_ => { SwitchTab(1); _tabSelected.OnNext(ItemType.Ingredient); }).AddTo(this);
            if (fishButton != null) fishButton.OnClickAsObservable()
                .Subscribe(_ => { SwitchTab(2); _tabSelected.OnNext(ItemType.Fish); }).AddTo(this);
            SwitchTab(0);
        }

        // 2. 使うボタン・閉じるボタンが押された時の処理
        if (useButton != null) useButton.OnClickAsObservable()
            .Subscribe(_ => _useClicked.OnNext(Unit.Default)).AddTo(this);
        if (closeButton != null) closeButton.OnClickAsObservable()
            .Subscribe(_ => _closeClicked.OnNext(Unit.Default)).AddTo(this);

        // 起動時は、詳細画面（DetailView）を隠しておく
        if (detailView != null) detailView.SetActive(false);

    }

    /// <summary>共通ナビゲーションのタブ番号を既存の通知へ変換する</summary>
    /// <param name="index">画面左から釣り具、魚、素材の順のタブ番号</param>
    /// <example>魚タブを確定した場合はFishを通知する</example>
    private void HandleTabSelected(int index) => _tabSelected.OnNext(
        index == 0 ? ItemType.Rod : index == 1 ? ItemType.Fish : ItemType.Ingredient);

    /// <summary>Viewが所有する通知ストリームを解放する</summary>
    /// <example>Prefabの破棄時にUnityが呼ぶ</example>
    private void OnDestroy()
    {
        _itemSelected.Dispose();
        _useClicked.Dispose();
        _tabSelected.Dispose();
        _closeClicked.Dispose();
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

    /// <summary>インベントリを表示し、選択枠を初期化する</summary>
    /// <example>Fで開くときにPresenterから呼ぶ</example>
    public void Show() 
    {
        gameObject.SetActive(true);
        _navigation?.SelectFirst();
    }

    /// <summary>インベントリ全体を非表示にする</summary>
    /// <example>FまたはEscで閉じるときに呼ぶ</example>
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
