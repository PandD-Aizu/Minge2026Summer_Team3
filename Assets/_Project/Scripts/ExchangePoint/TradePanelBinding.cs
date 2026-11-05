using System;
using System.Collections.Generic;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;
using Controller;
using ItemMenus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExchangePoint
{
    /// <summary>既存の商品一覧と詳細Prefabに取引データと操作を結び付ける</summary>
    public sealed class TradePanelBinding
    {
        private sealed class Offer
        {
            public ItemDefinition Item;
            public ExchangeRecipeDefinition Recipe;
            public int Mode;
            public TMP_Text Label;
        }
        private readonly Dictionary<Transform, Offer> _offers = new();
        private readonly ExchangePointService _service;
        private readonly TutorialController _tutorial;
        private readonly ItemMenuNavigation _navigation;
        private readonly TMP_Text _balance;
        private readonly TMP_FontAsset _font;
        private Offer _selected;
        private TMP_Text _detail;
        private Button _confirm;
        private TMP_Text _tradeTitle, _fishLabel, _receivedAmount, _givenAmount, _quantityLabel, _maximumLabel, _walletLabel;
        private TMP_Text _stationLabel;
        private Image _fishPicture, _receivedPicture;
        private TradeFlowArrow _receiveArrow, _giveArrow;
        private RectTransform _stationOrigin, _walletTarget;
        private ShellRewardEffect _rewardEffect;
        private int _quantity = 1;
        private int _lastFrame = -1;
        private float _nextInput;
        private bool _held;
        private string _result = "";

        /// <summary>既存一覧の枠を商品データから構築する</summary>
        /// <param name="root">ロード済み一覧</param><param name="navigation">既存カーソル</param>
        /// <param name="service">取引サービス</param><param name="tutorial">進行通知先</param><param name="open">詳細を開く操作</param>
        /// <example>一覧ロード後に一度生成する</example>
        public TradePanelBinding(GameObject root, ItemMenuNavigation navigation, ExchangePointService service,
            TutorialController tutorial, Action open)
        {
            _service = service;
            _tutorial = tutorial;
            _navigation = navigation;
            _font = Resources.Load<TMP_FontAsset>("Economy/TradeFont");
            navigation.ConfigureTradingTabs();
            ApplyWhiteTheme(root);
            var canvas = root.GetComponentInChildren<Canvas>(true).transform;
            _balance = Text(canvas, "Balance", new Vector2(0.85f, 0.92f), new Vector2(0.98f, 0.98f));
            _balance.color = Color.black;
            var icon = new GameObject("Shell", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(canvas, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.80f, 0.92f); iconRect.anchorMax = new Vector2(0.85f, 0.98f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            icon.GetComponent<Image>().sprite = Resources.Load<Sprite>("Economy/ShellCurrency");
            icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<Image>().raycastTarget = false;

            // 既存のグリッド、スクロール領域を維持して中身だけを差し替える
            for (int mode = 0; mode < navigation.Pages.Length; mode++)
            {
                var content = navigation.Pages[mode].content;
                var grid = content.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(240, 170);
                var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (Transform child in content) { child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject); }
                if (mode < 2)
                {
                    foreach (var item in service.Catalog.Items)
                        if (item != null && (mode == 0 ? item is FishDefinition : !(item is FishDefinition)))
                            AddOffer(content, new Offer { Item = item, Mode = mode }, open);
                }
                else
                {
                    foreach (var recipe in Resources.LoadAll<ExchangeRecipeDefinition>("Economy"))
                        if (recipe.CostFish != null && recipe.RewardItem != null)
                            AddOffer(content, new Offer { Item = recipe.RewardItem, Recipe = recipe, Mode = 2 }, open);
                }
            }
            Refresh();
        }

        /// <summary>商品枠を作りクリックと詳細選択を接続する</summary>
        /// <param name="parent">一覧Content</param><param name="offer">表示商品</param><param name="open">詳細表示</param>
        /// <example>タブごとの一覧生成に使う</example>
        private void AddOffer(Transform parent, Offer offer, Action open)
        {
            var row = new GameObject("TradeItem", typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = Color.white;
            StyleButton(row.GetComponent<Button>());
            offer.Label = Text(row.transform, "Label", new Vector2(0, 0), new Vector2(1, 0.42f));
            offer.Label.fontSize = 20;
            offer.Label.enableAutoSizing = true;
            offer.Label.fontSizeMin = 12;
            offer.Label.fontSizeMax = 20;
            var picture = new GameObject("ItemImage", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(row.transform, false);
            var rect = (RectTransform)picture.transform;
            rect.anchorMin = new Vector2(0.15f, 0.45f); rect.anchorMax = new Vector2(0.85f, 0.98f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            picture.GetComponent<Image>().sprite = offer.Item.ItemImage;
            picture.GetComponent<Image>().preserveAspect = true;
            picture.GetComponent<Image>().raycastTarget = false;
            _offers.Add(row.transform, offer);
            row.GetComponent<Button>().onClick.AddListener(() => { Select(row.transform); open(); });
        }

        /// <summary>選択商品を保持し数量を初期化する</summary>
        /// <param name="row">選択枠</param><example>カーソル決定時に呼ぶ</example>
        public void Select(Transform row)
        {
            if (row == null || !_offers.TryGetValue(row, out _selected)) return;
            _quantity = 1; _result = ""; _held = false;
        }

        /// <summary>一覧の所持数と残高を再表示する</summary>
        /// <example>画面表示時と取引後に呼ぶ</example>
        public void Refresh()
        {
            _balance.text = _service.Inventory.Balance.ToString("N0");
            bool saleVisibilityChanged = false;
            foreach (var pair in _offers)
            {
                var offer = pair.Value;
                int count = _service.Inventory.GetCount(offer.Item.ItemId);
                offer.Label.text = offer.Item.ItemName + "\n所持 " + count;

                // 売却一覧は所持中の魚だけを表示し、再入手時には同じ枠を再表示する
                if (offer.Mode != 0) continue;
                bool visible = count > 0;
                if (pair.Key.gameObject.activeSelf == visible) continue;
                pair.Key.gameObject.SetActive(visible);
                saleVisibilityChanged = true;
            }

            // 売り切った枠をカーソルの候補から外し、全数売却時はタブへ戻す
            if (saleVisibilityChanged && _navigation.ActiveTab == 0) _navigation.SelectFirst();
            UpdateDetail();
        }

        /// <summary>既存詳細パネルに数量操作と確定ボタンを接続する</summary>
        /// <param name="root">ロード済み詳細Prefab</param><example>詳細を開くたびに呼ぶ</example>
        public void BindDetail(GameObject root)
        {
            if (_detail == null)
            {
                ApplyWhiteTheme(root);
                // 既存の背景を残し、ダミー文章と操作ヒントを取引表示に置き換える
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                    if (image.name.IndexOf("Item", StringComparison.OrdinalIgnoreCase) >= 0) image.gameObject.SetActive(false);
                var canvas = root.GetComponentInChildren<Canvas>(true).transform;
                var panel = new GameObject("TradeBackground", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvas, false);
                var panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = new Vector2(0.20f, 0.15f);
                panelRect.anchorMax = new Vector2(0.80f, 0.85f);
                panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = Color.white;
                var content = panel.transform;
                _tradeTitle = Text(content, "Title", new Vector2(0.28f, 0.89f), new Vector2(0.68f, 0.98f));
                _tradeTitle.fontSize = 34;
                var walletIcon = Picture(content, "WalletIcon", new Vector2(0.76f, 0.90f), new Vector2(0.82f, 0.97f));
                walletIcon.sprite = Resources.Load<Sprite>("Economy/ShellCurrency");
                _walletTarget = walletIcon.rectTransform;
                _walletLabel = Text(content, "Wallet", new Vector2(0.82f, 0.90f), new Vector2(0.98f, 0.97f));
                _walletLabel.fontSize = 24;

                // 左の魚と右の集荷所を、入金と引き渡しの2本の矢印で結ぶ
                _fishPicture = Picture(content, "Fish", new Vector2(0.06f, 0.43f), new Vector2(0.30f, 0.66f));
                _fishLabel = Text(content, "FishName", new Vector2(0.02f, 0.32f), new Vector2(0.34f, 0.43f));
                _fishLabel.enableAutoSizing = true; _fishLabel.fontSizeMin = 16; _fishLabel.fontSizeMax = 26;
                var station = Picture(content, "CollectionPoint", new Vector2(0.72f, 0.43f), new Vector2(0.95f, 0.66f));
                station.color = Color.white;
                station.sprite = Resources.Load<Sprite>("Economy/CollectionCooler");
                _stationOrigin = station.rectTransform;
                _stationLabel = Text(station.transform, "Name", new Vector2(-0.1f, -0.35f), new Vector2(1.1f, -0.02f));
                _stationLabel.text = "集荷場";
                _receiveArrow = Arrow(content, "Receive", new Vector2(0.25f, 0.65f), new Vector2(0.76f, 0.77f), Color.black);
                _giveArrow = Arrow(content, "Give", new Vector2(0.25f, 0.34f), new Vector2(0.76f, 0.46f), Color.black);
                _receivedPicture = Picture(content, "ReceivedIcon", new Vector2(0.36f, 0.77f), new Vector2(0.46f, 0.88f));
                _receivedAmount = Text(content, "ReceivedAmount", new Vector2(0.46f, 0.77f), new Vector2(0.69f, 0.88f));
                _receivedAmount.color = Color.black;
                _receivedAmount.fontSize = 36;
                _givenAmount = Text(content, "GivenAmount", new Vector2(0.36f, 0.26f), new Vector2(0.64f, 0.34f));
                _givenAmount.fontSize = 32;
                _maximumLabel = Text(content, "Maximum", new Vector2(0.27f, 0.19f), new Vector2(0.65f, 0.26f));
                _maximumLabel.fontSize = 22;

                Button(content, "−", new Vector2(0.18f, 0.08f), new Vector2(0.29f, 0.17f), () => ChangeQuantity(-1));
                _quantityLabel = Text(content, "Quantity", new Vector2(0.30f, 0.08f), new Vector2(0.47f, 0.17f));
                Button(content, "＋", new Vector2(0.48f, 0.08f), new Vector2(0.59f, 0.17f), () => ChangeQuantity(1));
                _confirm = Button(content, "確定 [J]", new Vector2(0.69f, 0.08f), new Vector2(0.93f, 0.17f), Execute);
                _detail = Text(content, "TradeResult", new Vector2(0.22f, 0.48f), new Vector2(0.78f, 0.61f));
                _detail.fontSize = 22;
                var hint = Text(content, "Controls", new Vector2(0.10f, 0.01f), new Vector2(0.90f, 0.06f));
                hint.text = "A / D  数量変更     Esc  戻る"; hint.fontSize = 20;

                // 最前面の表示専用レイヤーで飛ばし、ボタン入力は遮らない
                var effect = new GameObject("ShellRewardEffect", typeof(RectTransform), typeof(ShellRewardEffect));
                effect.transform.SetParent(content, false);
                var effectRect = (RectTransform)effect.transform;
                effectRect.anchorMin = Vector2.zero; effectRect.anchorMax = Vector2.one;
                effectRect.offsetMin = effectRect.offsetMax = Vector2.zero;
                _rewardEffect = effect.GetComponent<ShellRewardEffect>();
            }
            UpdateDetail();
        }

        /// <summary>左右入力で数量を増減し長押しを一定間隔に制限する</summary>
        /// <param name="input">移動入力</param><example>詳細表示中に毎フレーム呼ぶ</example>
        public void NavigateQuantity(Vector2 input)
        {
            if (Mathf.Abs(input.x) < 0.5f) { _held = false; return; }
            if (_held && Time.unscaledTime < _nextInput) return;
            ChangeQuantity(input.x > 0 ? 1 : -1);
            _nextInput = Time.unscaledTime + (_held ? 0.12f : 0.35f); _held = true;
        }

        /// <summary>在庫または残高で可能な取引回数を返す</summary>
        /// <returns>実行可能な最大回数</returns><example>数量入力の上限に使う</example>
        private int Maximum()
        {
            if (_selected == null) return 0;
            if (_selected.Mode == 0) return _service.Inventory.GetCount(_selected.Item.ItemId);
            if (_selected.Mode == 1) return _selected.Item.PurchasePrice > 0 ? _service.Inventory.Balance / _selected.Item.PurchasePrice : 0;
            return _selected.Recipe.CostAmount > 0 ? _service.Inventory.GetCount(_selected.Recipe.CostFish.ItemId) / _selected.Recipe.CostAmount : 0;
        }

        /// <summary>数量を許容範囲内へ調整する</summary>
        /// <param name="delta">増減方向</param><example>＋ボタンは1を渡す</example>
        private void ChangeQuantity(int delta)
        {
            _quantity = (int)Math.Max(1L, Math.Min(Math.Max(1, Maximum()), (long)_quantity + delta));
            _result = ""; UpdateDetail();
        }

        /// <summary>数量、金額、必要素材、結果を表示する</summary>
        /// <example>数量変更後に呼ぶ</example>
        private void UpdateDetail()
        {
            if (_detail == null || _selected == null) return;
            var item = _selected.Item;
            bool exchange = _selected.Mode == 2;
            bool buying = _selected.Mode == 1;
            var source = exchange ? _selected.Recipe.CostFish : item;
            long given = exchange ? (long)_selected.Recipe.CostAmount * _quantity : _quantity;
            long received = exchange ? (long)_selected.Recipe.RewardAmount * _quantity
                : (long)(buying ? item.PurchasePrice : ((FishDefinition)item).SellPrice) * _quantity;

            _tradeTitle.text = exchange ? "直接交換" : buying ? "購入" : "売却";
            _fishPicture.sprite = source.ItemImage;
            _fishLabel.text = $"{source.ItemName}\n所持 × {_service.Inventory.GetCount(source.ItemId)}";
            _receivedPicture.sprite = exchange ? item.ItemImage : Resources.Load<Sprite>("Economy/ShellCurrency");
            _receivedAmount.text = $"× {received:N0}";
            _givenAmount.text = $"× {given:N0}";
            _quantityLabel.text = _quantity.ToString("N0");
            _maximumLabel.text = $"最大 {Maximum():N0}{(exchange ? "回" : source is FishDefinition ? "匹" : "個")}";
            _walletLabel.text = _service.Inventory.Balance.ToString("N0");
            _receiveArrow.Configure(buying, true);
            _giveArrow.Configure(!buying, false);
            _stationLabel.text = "集荷場";
            _detail.text = _result;
            _confirm.interactable = Maximum() >= _quantity;
        }

        /// <summary>現在の商品を一度確定し、成功時だけ進行を通知する</summary>
        /// <example>J入力または確定ボタンから呼ぶ</example>
        public void Execute()
        {
            if (_selected == null || _detail == null || !_detail.gameObject.activeInHierarchy || _lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            int balanceBefore = _service.Inventory.Balance;
            string error;
            bool success = _selected.Mode == 0 ? _service.TrySell((FishDefinition)_selected.Item, _quantity, out error)
                : _selected.Mode == 1 ? _service.TryBuy(_selected.Item, _quantity, out error)
                : _service.TryExchange(_selected.Recipe, _quantity, out error);
            _result = success ? "取引が完了した" : error;
            if (success && _selected.Mode != 1) _tutorial.NotifyExchangeCompleted();
            _quantity = Math.Min(_quantity, Math.Max(1, Maximum()));
            Refresh();

            // 保存済みの増加額だけを演出し、購入、直接交換、失敗では貝殻を増やさない
            int received = _service.Inventory.Balance - balanceBefore;
            if (success && received > 0)
                _rewardEffect.Play(_stationOrigin, _walletTarget, Resources.Load<Sprite>("Economy/ShellCurrency"), received);
        }

        /// <summary>背景とボタンを白、既存文字を黒へ揃える</summary>
        /// <param name="root">対象のUIルート</param><example>商品画像を生成する前に呼ぶ</example>
        private static void ApplyWhiteTheme(GameObject root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                image.color = image.transform.parent != null && image.transform.parent.name == "SelectionFrame"
                    ? Color.black : Color.white;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.color = Color.black;
            foreach (var button in root.GetComponentsInChildren<Button>(true)) StyleButton(button);
        }

        /// <summary>ボタンの全状態を白にし、黒い細線を付ける</summary>
        /// <param name="button">対象ボタン</param><example>数量ボタンの生成時に呼ぶ</example>
        private static void StyleButton(Button button)
        {
            var colors = button.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = colors.disabledColor = Color.white;
            button.colors = colors;
            AddOutline(button.gameObject);
        }

        /// <summary>白い面の境界を黒い細線で示す</summary>
        /// <param name="target">Imageを持つUI</param><example>集荷所の外枠に使う</example>
        private static void AddOutline(GameObject target)
        {
            var outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>画像や色付きの図形領域を作る</summary>
        /// <param name="parent">親</param><param name="name">名前</param><param name="min">左下</param><param name="max">右上</param>
        /// <returns>縦横比を保持するImage</returns><example>魚や貝殻の表示に使用する</example>
        private Image Picture(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        /// <summary>受け渡し方向を示す曲線矢印を作る</summary>
        /// <param name="parent">親</param><param name="name">名前</param><param name="min">左下</param><param name="max">右上</param><param name="color">矢印の色</param>
        /// <returns>方向を更新できる矢印</returns><example>売却の上下の矢印に使用する</example>
        private TradeFlowArrow Arrow(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TradeFlowArrow));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var arrow = go.GetComponent<TradeFlowArrow>(); arrow.color = color;
            return arrow;
        }

        /// <summary>既存フォントを使う文字領域を作る</summary>
        /// <param name="parent">親</param><param name="name">名前</param><param name="min">左下</param><param name="max">右上</param>
        /// <returns>生成した文字</returns><example>残高表示の生成に使う</example>
        private TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TMP_Text>(); text.font = _font; text.fontSize = 28;
            text.color = Color.black; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }

        /// <summary>文字付き操作ボタンを作る</summary>
        /// <param name="parent">親</param><param name="label">表示文字</param><param name="min">左下</param><param name="max">右上</param><param name="action">実行処理</param>
        /// <returns>生成したボタン</returns><example>数量変更ボタンに使う</example>
        private Button Button(Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.white;
            Text(go.transform, "Label", Vector2.zero, Vector2.one).text = label;
            var button = go.GetComponent<Button>(); button.navigation = new Navigation { mode = Navigation.Mode.None };
            StyleButton(button);
            button.onClick.AddListener(action); return button;
        }
    }
}
