# ゲーム開始時の操作チュートリアル実装手順書

## 目的

島への初回到着時にラジオを見せ、移動操作とインタラクト操作を順に案内する

既存の開始時会話にカメラ演出を接続する方法を使うため、`GameplayDialogueLifetimeScope` から開始処理へ `CameraZoom` を渡す設定を追加する

この文書は実装手順をまとめたものになる
掲載したコードや操作案内UIは、文書を保存した時点ではプロジェクトへ適用していない

## 1．今回作る演出

1. 島に到着する
2. ラジオへのズームを開始し、渚の独り言を表示する
3. 独り言を読み終えたら、通常カメラへ戻す
4. WASDの移動案内を表示する
5. ラジオに近づいたら、Jキーの案内へ切り替える
6. Jキーで既存のラジオ会話を開始する

この手順では、ズームと独り言を同時に始める
ズーム完了後に独り言を表示したい場合は、追加でカメラ切り替えの完了を待つ処理が必要になる

| 必要なもの | 対応 |
|---|---|
| ラジオへのズーム | 既存の `CameraZoom` を使用 |
| 独り言の表示 | 既存の会話表示を使用 |
| 開始会話とカメラの接続 | コードの追加が必要 |
| WASD・Jキーの案内表示 | 案内UIと表示切り替え処理の追加が必要 |
| ラジオへの接近判定 | 既存の `InteractableConnector` を使用 |
| 話しかけたあとの会話 | 既存の `DialogueTrigger` を使用 |

## 2．導入用の独り言を用意する

既存の開始会話を変更する場合は、[SO_CampStart.asset](C:/Unity_project/Minge2026Summer_Team3/Assets/_Project/SO/Dialogue/SO_CampStart.asset) を編集する

1. UnityのPlayモードを停止する
2. Projectウィンドウで `SO_CampStart` を選ぶ
3. Inspectorの `Lines` を開く
4. `Speaker` を `Player` にする
5. `Text` に導入の独り言を入力する

入力例

> あれは何だろう  
> 近づいてみよう

`Player` にすると、画面の話者名は「渚」になる

別の会話アセットとして作る場合は、Projectウィンドウの右クリックから `Create > Data > Dialogue > DialogueData` を選ぶ
作成後、Hierarchyの `GameplayDialogue` を選び、`GameplayDialogueLifetimeScope` の `Startup Dialogue` に指定する

同じセーブで繰り返さない会話にする場合は、`Play Once Per Save` をONにする
この設定は表示開始時に履歴を記録するため、中断した場合も次回は省略される

## 3．LifetimeScopeからカメラを渡せるようにする【追加が必要】

編集するファイルは [GameplayDialogueLifetimeScope.cs](C:/Unity_project/Minge2026Summer_Team3/Assets/_Project/Scripts/LifetimeScopes/GameplayDialogueLifetimeScope.cs)

まず、ほかの `[SerializeField]` が並んでいる場所に、次の参照欄を追加する

```csharp
[SerializeField, Tooltip("開始時の会話で使用するラジオ用カメラ切り替え")]
private CameraZoom _startupCamera;
```

これにより、UnityのInspectorでカメラ切り替え用コンポーネントを指定できるようになる

次に、`Configure` 内の既存の登録

```csharp
builder.RegisterEntryPoint<GameplayDialogueStartup>();
```

を、次の形へ変更する

```csharp
// 開始処理へ、このシーンのカメラ切り替えを渡す
builder.RegisterEntryPoint<GameplayDialogueStartup>()
    .WithParameter(typeof(CameraZoom), (object)_startupCamera);
```

`WithParameter` は、登録したクラスのコンストラクターへ、指定した値を渡すための設定になる
今回はこの方法で直接渡すため、`CameraZoom` を別途 `RegisterComponent` で登録する必要はない

手順3と手順4の両方を変更し終えるまでは、登録とコンストラクターの引数がそろわないため、動作確認はそのあとに行う

## 4．開始処理でカメラを受け取る【追加が必要】

編集するファイルは [GameplayDialogueStartup.cs](C:/Unity_project/Minge2026Summer_Team3/Assets/_Project/Scripts/Dialogue/GameplayDialogueStartup.cs)

`GameplayDialogueStartup` クラスのフィールドに追加する

```csharp
private readonly CameraZoom _cameraZoom;
```

次に、既存のコンストラクターを次の形へ変更する
最後の引数と代入が、カメラを受け取るための追加部分になる

```csharp
/// <summary>開始会話、帰還状態、暗転表示、カメラ切り替えを受け取る</summary>
/// <param name="controller">操作制限と会話再生を担当するController</param>
/// <param name="dialogue">通常のシーン開始時に再生する会話</param>
/// <param name="captureReturnDialogue">捕獲から帰還したときの会話</param>
/// <param name="progress">捕獲からの帰還状態を保持する進行データ</param>
/// <param name="fade">帰還時の暗転を解除するView</param>
/// <param name="cameraZoom">開始会話中のカメラ切り替え 未設定なら切り替えない</param>
/// <example>GameplayDialogueLifetimeScopeから生成する</example>
public GameplayDialogueStartup(
    GameplayDialogueController controller,
    DialogueData dialogue,
    CaptureReturnDialogue captureReturnDialogue,
    GameProgress progress,
    GameOverView fade,
    CameraZoom cameraZoom)
{
    _controller = controller;
    _dialogue = dialogue;
    _captureReturnDialogue = captureReturnDialogue;
    _progress = progress;
    _fade = fade;
    _cameraZoom = cameraZoom;
}
```

## 5．独り言の開始と終了にズームを接続する【追加が必要】

同じファイルの `StartAsync` 内にある、次の部分を探す

```csharp
var result = await _controller.PlayAsync(dialogue, cancellation);
if (result == DialogueResult.Rejected)
    Debug.LogWarning("シーン開始時の会話を再生できないため、会話データと入力状態を確認してください");
```

この部分だけを、次の処理へ置き換える
その前にある捕獲からの帰還判定、暗転解除、会話データ選択の処理は残す

```csharp
var zoomStarted = false;

try
{
    // 会話が実際に始まった場合に、ラジオ用カメラへ切り替える
    var result = await _controller.PlayAsync(dialogue, cancellation, () =>
    {
        // 捕獲後の帰還会話と、カメラ未設定のシーンでは切り替えない
        if (returningFromCapture ||
            _cameraZoom == null ||
            !_cameraZoom.isActiveAndEnabled)
        {
            return;
        }

        zoomStarted = true;
        _cameraZoom.SetZoom(true);
    });

    // 開始できなかった場合は設定確認用のログを出す
    if (result == DialogueResult.Rejected)
    {
        Debug.LogWarning(
            "シーン開始時の会話を再生できないため、会話データと入力状態を確認してください");
    }
}
finally
{
    // 会話の終了や中断時に通常カメラへ戻す
    if (zoomStarted && _cameraZoom != null)
    {
        _cameraZoom.SetZoom(false);
    }
}
```

この処理では、会話が始まった場合だけズームする
`Play Once Per Save` によって会話が省略された場合には、ズームも実行されない

会話中の移動制限は、既存の `GameplayDialogueController` が担当するため、ここへ追加する必要はない

## 6．Unityで参照を設定する

コードの変更がコンパイルされたあと、Unityで次の設定を行う

1. `CampStage` シーンを開く
2. Hierarchyで `GameplayDialogue` を選ぶ
3. `GameplayDialogueLifetimeScope` の `Startup Camera` を探す
4. Hierarchyの `RadioCameraZoom` をドラッグ＆ドロップする
5. `Startup Dialogue` に導入用の会話が指定されていることを確認する
6. シーンを保存する

`RadioCameraZoom` の `CameraZoom` コンポーネントでは、次の参照も確認する

| 項目 | 指定するもの |
|---|---|
| `Follow Player Camera` | `FollowPlayerCamera` のCinemachine Camera |
| `Zoom Camera` | `RadioZoomCamera` のCinemachine Camera |

通常用とズーム用には、別々のカメラを指定する

### 映り方と切り替え時間の調整

| 調整したい内容 | 設定する場所 |
|---|---|
| ラジオを撮影する位置 | `RadioZoomCamera` の `Transform > Position` |
| ラジオを撮影する方向 | `RadioZoomCamera` の `Transform > Rotation` |
| 映る範囲の広さ | `RadioZoomCamera` の `Cinemachine Camera > Lens > Field Of View` |
| カメラ切り替えの時間 | `Main Camera` の `Cinemachine Brain > Default Blend > Time` |

現在の設定では、ズーム用カメラの `Field Of View` は `60`、切り替え時間は `0.6` 秒になっている

ここまでで「開始時にラジオへズームし、独り言を表示して、読み終わったら通常カメラへ戻る」部分ができる
操作案内UIは、次の手順で別途追加する

## 7．移動とインタラクトの案内をつなぐ【別途実装が必要】

ここからは、操作案内用のUIと切り替え処理を追加する
以下は実装する動作の説明であり、手順3〜6のコードだけで案内UIが表示されるわけではない

既存の会話ウィンドウは、表示中に移動を止める
そのため、「WASDで移動」と表示しながら歩いてもらう部分は、会話ウィンドウとは別の案内UIを使う

表示の切り替えは、次のようにする

| タイミング | 表示内容 |
|---|---|
| 導入の独り言が正常終了したあと | `WASDで移動してラジオに近づこう` |
| ラジオの操作範囲に入ったとき | `Jキーでラジオを調べる` |
| 操作範囲から離れたとき | 移動案内へ戻す |
| 最初のラジオ会話が始まったとき | 操作案内を非表示にする |

### 接近判定に使用するもの

接近判定は [InteractableConnector.cs](C:/Unity_project/Minge2026Summer_Team3/Assets/_Project/Scripts/InteractableObject/InteractableConnector.cs) の次の情報を使える

- `IsPlayerNearby`：現在近くにいるか
- `OnPlayerNearbyChanged`：近づいた・離れたときの通知

案内UIの初期表示では `IsPlayerNearby` を確認し、その後の変化は `OnPlayerNearbyChanged` で受け取る
これにより、案内を表示した時点で既にラジオの近くにいた場合にも対応できる

### 既存のラジオ会話につなぐ

Jキーでラジオ会話を開始する処理は、既存の `DialogueTrigger` を使えるため、重複して作る必要はない

導入の独り言を読み終えた時点では、チュートリアルの段階を進めない
既存の最初のラジオ会話を読み終えるまでは `TalkToRadioFirst` を維持する

操作案内が完了したかどうかの管理は、会話データの `Play Once Per Save` とは別に考える
会話が表示済みとして省略される場合にも移動案内を再開する必要があるか、チュートリアルの進行状態に合わせて決める

## 8．動作確認

新規のゲーム状態で、次の順番に確認する

1. 開始時にラジオへズームする
2. 渚の独り言が表示される
3. Jキーまたはウィンドウのクリックで読み終える
4. 通常カメラへ戻り、移動できるようになる
5. WASDの案内が表示される
6. ラジオに近づくとJキーの案内に変わる
7. Jキーで既存のラジオ会話が始まる
8. 会話終了後、元のゲーム進行へつながる

動作確認の5以降は、手順7の案内UIと切り替え処理を実装したあとに確認する

会話を始めた直後は、一度Jキーを離してから再度押す
開始時の入力で最初のセリフが飛ばないようにする処理が入っている

## 9．うまく動かないときの確認表

| 症状 | 確認すること |
|---|---|
| `Startup Camera` が表示されない | コードのコンパイルが完了しているか、Consoleにエラーがないか |
| 会話は表示されるがズームしない | `Startup Camera` に `RadioCameraZoom` を指定したか |
| カメラ切り替え時にエラーが出る | 通常用とズーム用に別々のCinemachine Cameraを指定したか |
| ズーム先がラジオを映していない | `RadioZoomCamera` の位置と向きが合っているか |
| 独り言が表示されない | `Startup Dialogue` が設定され、`Lines` にセリフがあるか |
| 一度表示した独り言が再表示されない | `Play Once Per Save` によって表示済みになっていないか |
| WASD・Jキーの案内が出ない | 手順7の案内UIと表示切り替え処理を実装したか |
| 案内を表示したまま移動できない | 操作案内に、移動を止める会話再生を使っていないか |

繰り返し確認する場合は、テスト用の会話アセットを用意して `Play Once Per Save` をOFFにする方法もある
その場合は、初回以外のシーン開始時にも会話とズームが再生される点を考慮する
