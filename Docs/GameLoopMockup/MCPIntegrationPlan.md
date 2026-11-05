# Unity開発用MCPの導入状況

更新日: 2026-10-29
状態: Codex設定修正済み / MCPクライアントからUnity Editorへの読み取り接続を確認済み

## 調査結果と修正内容

MCP for UnityのパッケージとPythonサーバーはすでに導入されていた
Unity側はSTDIO方式だったが、Codexの `unityMCP` は起動していない `http://127.0.0.1:8080/mcp` を参照していた
Codexの既存登録をSTDIO方式へ修正し、サーバーをEditor側と同じ `10.3.0` に固定した

| 項目 | 確認結果 |
|---|---|
| Unity Editor | `6000.6.0f1`、このプロジェクトで起動中 |
| Editor側パッケージ | `com.coplaydev.unity-mcp`、PackageCache上の版は `10.3.0` |
| パッケージ参照 | manifestは `#main`、lockのGit hashは `81542a7e86292b5c4c21b1782c5af23b567f45ce` |
| Pythonサーバー | `mcpforunityserver==10.3.0` |
| 接続方式 | Codex → STDIOサーバー → ローカルのUnityブリッジ |
| Unityブリッジ | このPCでの検証時は `127.0.0.1:6401`、固定値として設定する必要はない |
| 対象インスタンス | `Minge2026Summer_Team3@e44451ff` |
| Unity Pipeline | `com.unity.pipeline` の `0.7.0-exp.1` は登録済み、今回の接続には使用していない |
| Unity CLI | 今回のシェルではPATH上に見つからず、追加インストールは行っていない |

今回変更したのは、個人用Codex設定の `unityMCP` 登録とこの文書
既存のシーン、ゲームコード、パッケージ参照、Unityの接続方式は変更していない

## Codexの設定

このPCでは `C:/Users/dream/.codex/config.toml` の既存セクションを次の内容に変更した
このファイルはユーザー共通設定なので、ほかのプロジェクトからも同じ登録を利用する
ほかのMCP登録やCodex設定は保持している

```toml
[mcp_servers.unityMCP]
command = "C:/Users/dream/.local/bin/uvx.exe"
args = ["--from", "mcpforunityserver==10.3.0", "mcp-for-unity", "--transport", "stdio"]
startup_timeout_sec = 60
```

上の実行ファイルの絶対パスはこのPC用
別のPCでは `Get-Command uvx` などで実際の場所を確認して置き換える
初回の起動には依存パッケージの展開時間が必要だったため、起動待ちを60秒に設定した

変更前の設定は同じフォルダーの `config.toml.before-unityMCP-stdio-20261029-174956.bak` に保存した
戻す場合は、その後の設定変更がないことを確認してバックアップを復元する
その後に別の設定を追加している場合は `unityMCP` セクションだけを戻す

Codexの設定方法は [OpenAI公式資料](https://learn.chatgpt.com/docs/extend/mcp) を参照
MCP for Unityの仕様は [開発元の資料](https://github.com/CoplayDev/unity-mcp) を参照

## 実施した接続確認

保存済みのCodex設定から実行コマンドと引数を読み込み、MCP Python SDKのクライアントで検証した
診断用クライアントは検証終了時に、自分で起動したサーバーを終了した

| 確認項目 | 結果 |
|---|---|
| `codex mcp get unityMCP --json` | 有効なSTDIO登録、起動待ち60秒として認識 |
| MCP初期化 | 成功 |
| ツール・リソース一覧 | ツール48件、リソース18件 |
| `mcpforunity://instances` | 対象プロジェクトを1件検出 |
| `set_active_instance` | このプロジェクトを明示して成功 |
| `mcpforunity://project/info` | プロジェクトルートが `C:/Unity_project/Minge2026Summer_Team3` と一致 |
| `mcpforunity://editor/state` | アクティブシーンは `CampStage`、編集可能、Play停止中 |
| `manage_scene` の `get_hierarchy` | `CampStage` のルート階層を取得 |
| `read_console` の `get` | 実行時点のConsoleエラーは0件 |

MCPサーバーの初期化応答に含まれる `3.4.11` はFastMCPの版
今回固定したPythonサーバーパッケージの版 `10.3.0` とは別の値

## 利用手順

1. Unityでこのプロジェクトを開く
2. UnityのMCP for Unity設定をSTDIO方式にする、このPCでは設定済み
3. Codexを再起動して変更後の設定を読み込み直す
4. `mcpforunity://instances` で対象を確認し、`set_active_instance` で選択する
5. シーンやConsoleの取得から利用する

STDIOサーバーはCodexが必要に応じて起動するため、別のHTTPサーバーを手動起動する必要はない
複数のEditorを開いている場合は、操作前に必ず対象インスタンスを選択する
プロジェクトの保存場所が変わるとインスタンスのhashも変わるため、一覧から選び直す

今回の診断接続と、実行中のCodexチャットへのツール反映は別
現在のチャットには `unityMCP` ツールが公開されていないため、アプリを再起動して読み込み直す

## 未実施の確認

- 作業用シーンでのオブジェクト作成・移動・削除
- コード再コンパイル、Play終了、Editor再起動後の再接続
- Codexアプリ再起動後にチャットから実際にツールを呼び出す確認

今回はMCPの調査と設定に限定し、これらの編集・再起動試験は実施していない
Editor側パッケージの参照は引き続き `#main` のため、将来更新した際はPythonサーバーの固定版も合わせて見直す
