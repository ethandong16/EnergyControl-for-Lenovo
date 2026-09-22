# EnergyControl for Lenovo

[English](README.md) | [简体中文](README.zh-CN.md) | 日本語

[![CI](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml/badge.svg)](https://github.com/ethandong16/EnergyControl-for-Lenovo/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/ethandong16/EnergyControl-for-Lenovo?include_prereleases)](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases)
[![License](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

対応する Lenovo ノート PC の充電、パフォーマンス、キーボードのバックライトを管理する Windows 用ポータブルツールです。1 つの実行ファイルで、中国語・英語・日本語の GUI とコマンドラインを利用できます。

コミュニティが開発する非公式ソフトウェアです。Lenovo の非公開 DLL、テレメトリ、常駐サービス、インストーラー、自動更新は含みません。

## はじめに

1. [Releases](https://github.com/ethandong16/EnergyControl-for-Lenovo/releases) から Windows x64 用 ZIP をダウンロードし、展開します。
2. `Get-FileHash .\<ダウンロードしたファイル>.zip -Algorithm SHA256` で SHA-256 を計算し、付属のチェックサムと照合します。
3. `EnergyControl.exe` をダブルクリックして GUI を開くか、PowerShell で `.\EnergyControl.exe diagnose` を実行します。

Windows 10/11 x64 と .NET Framework 4.8 が必要です。配布ファイルは未署名です。`main` ブランチには、最新リリースにまだ含まれていない変更がある場合があります。

## 機能と依存関係

| 機能 | 設定 | 実行時の依存関係 |
| --- | --- | --- |
| 充電モード | 通常、バッテリー保護、急速充電 | 対応する Lenovo EnergyDrv / ACPIVPC ドライバー。Addin による代替経路も利用可能 |
| 充電しきい値（実験的） | 充電開始・停止の割合 | Lenovo Power RPC サービスと対応ファームウェア |
| パフォーマンス（実験的） | 自動、静音、高性能、エキスパート／クリエイター | インストール済みの対応 Lenovo Addin |
| バックライト（実験的） | オフ、明るさ 1・2、自動、状態の記憶、自動減光、初期設定への復元 | インストール済みの対応 IdeaNotebookAddin とファームウェア |
| 診断 | 利用可否、状態、エラー | 各インターフェースを個別に照会 |

利用可能な設定は機種によって異なります。バッテリー保護モードの上限はファームウェアによって決まり、**任意の充電割合を設定できるとは限りません**。パフォーマンスの自動切り替えは CLI からも操作できます。

バックライトの読み取りは IdeaNotebookAddin `1.0.13.79` で確認済みです。検証機は `TwoLevelsAuto` に対応し、自動減光には非対応でした。要求データの構築はテスト済みですが、各機種でのハードウェア書き込みは未検証です。[互換性](COMPATIBILITY.md)と[バックライトの調査記録](KEYBOARD_BACKLIGHT_INTERFACE.md)も参照してください。

## デスクトップ画面

起動時に Windows の表示言語を使用します。中国語（`zh-*`）は簡体字中国語、日本語（`ja-*`）は日本語、それ以外は英語になります。日付や数値の地域設定では GUI の言語は変わりません。

**ヘルプ**ボタンは、付属の `README.html` を GUI と同じ言語で開きます。[README.html](README.html) を直接開くとブラウザーの優先言語が選ばれ、手動でも切り替えられます。GitHub の Markdown 表示では OS の言語を検出できないため、ページ上部のリンクを使用してください。ローカルのヘルプがない場合は、ブラウザーで該当言語の GitHub README を開きます。

バッテリー、パフォーマンス、キーボード、診断を別々のタブに配置しています。更新ボタンと状態表示は常に表示されます。モードの選択状態は直近の読み取り結果を反映し、バックライトのオン／オフ設定にはチェックボックスを使用します。非対応の操作は無効になり、利用できないしきい値の編集欄は非表示になります。

変更には確認が必要です。適用後に状態を再読み取りします。診断タブには完全なエラー情報と最終読み取り時刻が残ります。タブの切り替えではハードウェアにアクセスしません。

![キーボード設定](docs/images/keyboard.ja.png)

*現在のソースから生成した GUI。レイアウト検証用の模擬データを使用しています。実際の操作項目は機種によって異なります。*

## コマンドライン

読み取り専用の照会：

```powershell
.\EnergyControl.exe status
.\EnergyControl.exe diagnose
.\EnergyControl.exe charge direct get
.\EnergyControl.exe charge threshold get
.\EnergyControl.exe performance get
.\EnergyControl.exe keyboard-backlight get
.\EnergyControl.exe keyboard-backlight capability
```

書き込みには必ず `--apply` が必要です。以下は独立した例です。必要なコマンドだけを実行してください。

```powershell
.\EnergyControl.exe charge direct set conservation --apply
.\EnergyControl.exe charge threshold set 75 80 --apply
.\EnergyControl.exe performance set quiet --apply
.\EnergyControl.exe performance auto-transition on --apply
.\EnergyControl.exe keyboard-backlight set level1 --apply
.\EnergyControl.exe keyboard-backlight reserve on --apply
.\EnergyControl.exe keyboard-backlight auto-dim on --apply
.\EnergyControl.exe keyboard-backlight restore-default --apply
```

| コマンド | 値 |
| --- | --- |
| `charge direct set` または `charge set` | `normal`、`conservation`、`express` |
| `performance set` | `auto`、`quiet`、`performance`、`geek` |
| `keyboard-backlight set` | `off`、`level1`、`level2`、`auto` |
| `reserve`、`auto-dim`、`performance auto-transition` | `on`、`off` |

`charge get/set` は Addin、`charge direct get/set` は EnergyDrv を使用します。`backlight` は `keyboard-backlight` の別名です。`help` でコマンド一覧を表示できます。終了コードは `0` が成功、`1` が操作失敗または `--apply` の不足、`2` がコマンドまたはモードの誤りです。

## トラブルシューティング

| 症状 | 確認事項 |
| --- | --- |
| IdeaNotebookAddin が見つからない | 対応する Lenovo Vantage コンポーネントをインストールまたは修復してください。Baiying だけでは、この Addin が導入されているとは限りません。 |
| Power RPC エラー `1722` | 必要なサービスを利用できません。ドライバー直接制御による充電設定は独立して使用できます。 |
| EnergyDrv を開けない | Lenovo ACPIVPC ドライバーとアクセス権限を確認してください。 |
| 設定が利用できない、または拒否される | 診断とファームウェアの対応状況を確認してください。他の機種の対応状況から推測しないでください。 |

統合テストでは `LENOVO_SETTINGS_ADDIN_PATH` と `LENOVO_POWER_RPC_PATH` に、信頼できるインストール済みアセンブリまたはそのディレクトリを指定できます。

## ビルドと検証

Windows で SDK 形式の `net48` プロジェクトをビルドできる .NET SDK と、テストスクリプト用の PowerShell 7 を使用します。復元時に公開の .NET Framework 参照アセンブリを取得します。コンパイルに Lenovo DLL は不要です。

```powershell
.\build.ps1
.\tests\run-tests.ps1
.\verify-layout.ps1
```

実行ファイルは `artifacts\publish\EnergyControl.exe` に出力されます。再ビルドの前に、実行中のビルド成果物を終了してください。`-Clean` は以前のビルド出力を削除します。

プロトコルと依存関係のテストに Lenovo ハードウェアは不要です。Addin がない場合は実際の要求データ構築テストをスキップします。GUI テストは 3 言語の模擬状態を使用し、4 タブ、狭い／広いウィンドウ、100/150/200% の拡大率を検証します。画像は `artifacts\layout\<言語>` に保存します。テストでハードウェア設定を変更することはありません。

ビルド時には 3 つの Markdown ファイルからオフライン HTML ヘルプも生成します。README を編集した後、PowerShell 7 で `.\docs\build-readme.ps1 -OutputPath .\README.html` を実行すると、リポジトリ内の HTML を更新できます。

| ソース | 役割 |
| --- | --- |
| `Gui.cs` | UI の状態、確認、非同期操作 |
| `Gui.Layout.cs` | タブ、コントロール、診断表示 |
| `GuiDeviceClient.cs` | デバイス状態とインターフェースの集約 |
| `UiText.cs` | 翻訳と表示言語の選択 |
| `Program.cs` | CLI |
| `DirectChargeMode.cs`、`ChargeThreshold.cs`、`Models.cs`、`KeyboardBacklight.cs` | デバイスバックエンド |

## プロジェクト資料

[インターフェース](INTERFACES.md) · [充電機能の調査](REVERSE_ENGINEERING.md) · [貢献](CONTRIBUTING.md) · [セキュリティ](SECURITY.md) · [免責事項](DISCLAIMER.md)

[GPL-3.0-only](LICENSE) ライセンスです。Lenovo の名称は互換性を示すためにのみ使用しています。本プロジェクトは Lenovo と提携しておらず、Lenovo の承認を受けたものではありません。
