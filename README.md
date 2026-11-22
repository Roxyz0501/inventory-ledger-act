# Inventory Ledger for ACT

作者: Roxyz0501

**開発版です。実ゲームでの取得検証は未完了で、依頼された全機能はまだ完成していません。**

ACT＋FFXIV_ACT_Pluginの`NetworkReceived`を購読し、所持品をローカルへ記録するACTプラグインです。Teamcraftの通信による所持品取得方式を参考にしています。TeamcraftやDalamudのインストールは不要です。ゲームへの操作・通信送信・独自DLL注入は行いません。

## 現在の実装範囲

| 機能 | 状態 |
| --- | --- |
| キャラクター別所持品 | 通信解析・保存を実装、実ゲーム未検証 |
| バッグ・装備・アーマリー・クリスタル・通常／追加チョコボかばん | 既知コンテナに対応する解析を実装、実ゲーム未検証 |
| リテイナー一覧・所持品・ギル・出品価格 | 解析を実装。自身のリテイナー名とNPC出現による所有者対応付けは実ゲーム検証が必要 |
| FCチェスト | FC IDによる共有所有者として解析。複数キャラクターからの二重集計を防止。実ゲーム未検証 |
| 横断検索・一覧・アイコン表示・CSV出力 | 実装済み。アイコン表示は実際のゲームの並び順や空きスロットを再現するものではありません |
| ギル残高の差分 | 同一取得セッションの既知残高からの変更のみを記録。用途不明として表示 |
| 自分のリテイナーの販売履歴 | **未実装**。市場全体の取引履歴とは別の解析が必要 |
| 成立したトレードの品目・相手・ギル | **未実装**。取消と成立を区別する実データが必要 |
| ギル用途別集計 | **未実装**。残高差だけで購入・消費・収益・資金移動を推測しません |
| 愛蔵品キャビネット・ミラージュドレッサー | 未対応 |

完全なスナップショットの項目数が揃うまで既存の所持品を消しません。未観測・部分取得・保存済みを区別し、保存済み情報には最終確認日時を表示します。非ログインキャラクターの最新値や導入前の全取引を復元する機能ではありません。

## 導入と試験取得

1. 同じフォルダへ`InventoryLedger.dll`、`InventoryLedger.Updater.exe`、`README.md`を展開してください。
2. ACTでFFXIV_ACT_Pluginを有効にしてから、Pluginsのプラグイン追加から`InventoryLedger.dll`を登録します。
3. 設定画面で言語を選択できます。初回のみOSのUI言語を反映し、その後は保存した選択を保持します。英語がフォールバックです。
4. この開発版の通信定義は**Global 7.56h**です。対象パッチと一致する環境でのみ、設定の入力欄へ`7.56h`を入力し「このビルドを許可」を押し、試験的な取得を有効にします。**入力は互換性検証の代わりにはなりません。異なるゲームパッチでは有効にしないでください。**
5. 有効化後に再ログインします。ログイン時のContent IDを取得するまで、所有者を推測して保存しません。各保管場所、リテイナー、FCチェストを開いて取得状況を確認してください。
6. ゲームのビルド文字列が変わると取得を停止します。通信定義と実データを検証してから再対応します。

ACTやゲームへ自動登録・起動はしません。CN/KR版のゲーム用通信定義はまだ含みません。UIの4言語対応とは別です。

「所持品」の検索は保存済みデータ全体または選択所有者を対象にします。CSVは現在の絞り込み結果を出力します。ギル残高は通貨コンテナにも表示されます。アイテム名・アイコンはXIVAPI v2から非同期取得してキャッシュします。送信するのはアイテムID・言語・アイコンパスで、キャラクターやリテイナー名・所持数は送信しません。日本語以外のアイテム名は英語です。通信失敗時はアイテムIDを表示します。

## 保存・復旧

保存先は`%APPDATA%/Advanced Combat Tracker/InventoryLedger/`です。`ledger.json`が台帳、`settings.xml`が設定、`Items/`が表示用キャッシュです。台帳を原子的に置換し、`.bak`へ前回データを保存します。破損時はバックアップを読み、破損ファイルを日時付きで保全します。両方読み取れない場合は取得・保存を停止します。ファイルの手動修復はACTを停止してから行ってください。

台帳は自分のプレイデータです。GitHubや配布ZIPに含めないでください。未対応の未知パケットを台帳の販売・トレードに変換することはありません。

## 更新

更新元: https://github.com/Roxyz0501/inventory-ledger-act

「更新」タブで現在版・最新版・状態・更新内容を確認できます。起動時確認は既定ONで設定から解除可能です。確認は非同期、draftとprereleaseは対象外です。この開発版だけが存在する間は「安定版Releaseがありません」と表示されます。

ダウンロードは「更新をダウンロードして準備」を押した時だけ行います。HTTPS、所有リポジトリのRelease asset URL、ZIP許可リスト、SHA-256、アセンブリ名、会社名、Releaseバージョンを確認します。ACT登録の実DLLパスを優先し、ロード中DLLを直接上書きしません。検証したパッケージ内UpdaterがACT終了を待ち、バックアップを取って置換します。失敗時は復元し、適用後はACTを再起動してください。

## ビルドとRelease

Windows、.NET SDK、.NET Framework 4.8 targeting pack、ACTが必要です。

```powershell
./build.ps1 -ActPath 'ACTの配置先/Advanced Combat Tracker.exe'
```

ビルド、合成パケットによる回帰テスト、4言語の画面生成、配布物のハッシュ・アセンブリ検証を行います。実ゲームの通信を記録して再生したテストではありません。

配布物は`InventoryLedger-vX.Y.Z.zip`と`InventoryLedger-vX.Y.Z.sha256`。ZIPは`InventoryLedger.dll`、`InventoryLedger.Updater.exe`、本READMEの3ファイルのみです。通信定義はDLLへ埋め込みます。SHA-256マニフェストは`<64桁小文字hash>  <ZIP名>`形式です。

GitHub Actionsはmain/PR/tagでビルドとテストを実行します。外部ActionはコミットSHAに固定しています。初回は`v0.1.0-alpha.1`タグからprereleaseを生成します。安定版の`vX.Y.Z`タグは、実ゲーム検証後にリポジトリ変数`LIVE_CAPTURE_VERIFIED=true`が設定されるまで公開を拒否します。販売履歴・トレード・用途別収支の実装と実データ検証が残っています。

## 任意支援

[Ko-fiでRoxyz0501の開発を支援](https://ko-fi.com/roxyz0501)できます。支援は完全に任意で、支援しなくてもすべての機能が使えます。機能差はありません。支援タブのボタンを明示的に押した場合のみ既定ブラウザを開きます。

## 参考資料とライセンス

本プラグイン独自部分のライセンスは未設定です。以下の第三者部分のライセンスを変更するものではありません。

- Teamcraftの取得方式: https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/staging/apps/client/src/app/modules/inventory/inventory.service.ts
- Teamcraftのリテイナー情報処理: https://github.com/ffxiv-teamcraft/ffxiv-teamcraft/blob/staging/apps/client/src/app/core/electron/retainers.service.ts
- 参照したパケットレイアウト: https://github.com/ffxiv-teamcraft/pcap-ffxiv/tree/595a150175d6355fb0aca73e14619b1bc5c844a5/src/packet-processors/processors
- 埋め込み通信識別子の取得元: https://github.com/karashiiro/FFXIVOpcodes （Global 7.56h、2026-09-27取得）
- アイテム表示データ: https://v2.xivapi.com/

Teamcraft本体のコードは同梱していません。以下は参照したMITライセンスのパケット解析・通信識別子の著作権表示です。

MIT License

Copyright (c) 2021 karashiiro (pcap-ffxiv)

Copyright (c) 2020 karashiiro (FFXIVOpcodes)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
