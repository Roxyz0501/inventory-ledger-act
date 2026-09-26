namespace InventoryLedger
{
    public static partial class Localization
    {
        private static void AddInventoryResources()
        {
            var rows = new[] {
                new[] {"Subtitle","Inventory, retainers and observed balances — experimental","所持品・リテイナー・観測した残高 — 開発版","物品、雇员与已观测余额 — 实验版","소지품, 집사 및 관측 잔액 — 실험 버전"},
                new[] {"Inventory","Inventory","所持品","物品","소지품"},
                new[] {"CaptureEnabled","Enable experimental capture","試験的な取得を有効にする","启用实验性采集","실험적 수집 사용"},
                new[] {"CaptureHint","Profile: Global {0}. Live compatibility is unverified. Enter the exact patch below to allow this game build. Log in again after enabling. Open each bag, retainer and FC chest to observe contents.","通信定義: Global {0}。実ゲームでの互換性は未確認です。対応パッチを下欄へ入力すると現在のゲームビルドを許可します。有効化後に再ログインし、各かばん・リテイナー・FCチェストを開いてください。","协议: Global {0}。尚未验证实机兼容性。输入准确补丁版本以允许当前游戏版本。启用后重新登录并打开各背包、雇员和部队箱。","프로필: Global {0}. 실제 게임 호환성 미검증. 정확한 패치를 입력하여 현재 빌드를 허용하세요. 사용 후 다시 로그인하고 가방, 집사 및 부대 금고를 여세요."},
                new[] {"ApproveCapture","Allow this build","このビルドを許可","允许此版本","이 빌드 허용"},
                new[] {"CaptureBuild","Game build: {0} / rejected: {1}","ゲームビルド: {0} / 解析除外: {1}","游戏版本: {0} / 已拒绝: {1}","게임 빌드: {0} / 제외: {1}"},
                new[] {"AwaitPlugin","Waiting for FFXIV_ACT_Plugin.","FFXIV_ACT_Pluginの起動を待っています。","等待FFXIV_ACT_Plugin。","FFXIV_ACT_Plugin 대기 중."},
                new[] {"AwaitLogin","Waiting for login data. Please log in again.","ログインデータ待ちです。再ログインしてください。","等待登录数据，请重新登录。","로그인 데이터 대기 중. 다시 로그인하세요."},
                new[] {"Capturing","Receiving experimental inventory data.","所持品データを試験取得中です。","正在实验性采集物品数据。","소지품 데이터를 실험적으로 수집 중입니다."},
                new[] {"Disabled","Capture is off. Saved records remain available.","取得は停止中です。保存済み記録は閲覧できます。","采集已关闭，可查看已保存记录。","수집이 꺼져 있습니다. 저장 기록은 볼 수 있습니다."},
                new[] {"BuildBlocked","Capture paused: allow the supported build in Settings. Only Global is supported.","取得停止: 設定で対応ビルドを許可してください。Global版のみ対応します。","采集暂停：请在设置中允许支持的版本。仅支持Global。","수집 중지: 설정에서 지원 빌드를 허용하세요. Global만 지원합니다."},
                new[] {"Incomplete","Some packets were missing; old snapshots were retained.","受信データが不足したため以前の記録を保持しています。","部分数据缺失，已保留旧记录。","일부 데이터가 누락되어 이전 기록을 유지했습니다."},
                new[] {"SaveFailed","Could not save data. Existing files are preserved.","保存できませんでした。既存ファイルは保持しています。","保存失败，已有文件已保留。","저장 실패. 기존 파일은 유지됩니다."},
                new[] {"LoadFailed","Data could not be read; capture and saving are disabled to preserve files.","データを読めないため、ファイル保護のため取得・保存を停止しています。","无法读取数据，已停止采集和保存以保护文件。","데이터를 읽을 수 없어 파일 보호를 위해 수집 및 저장을 중지했습니다."},
                new[] {"Recovered","Recovered from backup. The damaged file has been preserved.","バックアップから復旧しました。破損ファイルは退避しています。","已从备份恢复，并保留损坏文件。","백업에서 복구했습니다. 손상된 파일은 보존했습니다."},
                new[] {"All","All owners","全所有者","全部所有者","모든 소유자"},
                new[] {"Search","Search name / item ID / owner","名前・アイテムID・所有者で検索","搜索名称 / 物品ID / 所有者","이름 / 아이템 ID / 소유자 검색"},
                new[] {"List","List","一覧","列表","목록"},
                new[] {"Grid","Bag view","バッグ表示","背包视图","가방 보기"},
                new[] {"Retainers","Retainers","リテイナー","雇员","집사"},
                new[] {"History","Balance log","残高記録","余额记录","잔액 기록"},
                new[] {"Export","Export visible CSV","表示中をCSV出力","导出当前CSV","현재 CSV 내보내기"},
                new[] {"ExportDone","CSV exported.","CSVを出力しました。","CSV已导出。","CSV를 내보냈습니다."},
                new[] {"ExportFailed","Could not export CSV.","CSVを出力できませんでした。","无法导出CSV。","CSV를 내보낼 수 없습니다."},
                new[] {"Owner","Owner","所有者","所有者","소유자"},
                new[] {"Item","Item","アイテム","物品","아이템"},
                new[] {"Location","Location","保管場所","存放位置","보관 장소"},
                new[] {"Slot","Slot","スロット","格位","슬롯"},
                new[] {"Quantity","Quantity","数量","数量","수량"},
                new[] {"Updated","Last observed","最終確認","最后观测","마지막 확인"},
                new[] {"State","State","状態","状态","상태"},
                new[] {"Cached","Saved snapshot","保存済み","已保存快照","저장된 기록"},
                new[] {"InferredOwner","Saved (owner inferred from spawn)","保存済み（出現名から所有者推定）","已保存（根据出现名称推定所有者）","저장됨 (등장 이름으로 소유자 추정)"},
                new[] {"Partial","Partial","一部のみ","部分","일부"},
                new[] {"Character","Character","キャラクター","角色","캐릭터"},
                new[] {"Retainer","Retainer","リテイナー","雇员","집사"},
                new[] {"Fc","FC chest","FCチェスト","部队箱","부대 금고"},
                new[] {"Saddlebag","Chocobo bag","チョコボかばん","陆行鸟鞍囊","초코보 가방"},
                new[] {"Armoury","Armoury chest","アーマリーチェスト","兵装库","장비함"},
                new[] {"Equipment","Equipment","装備中","装备","장비"},
                new[] {"Currency","Currency","通貨","货币","화폐"},
                new[] {"Crystals","Crystals","クリスタル","水晶","크리스탈"},
                new[] {"Bags","Bag","バッグ","背包","가방"},
                new[] {"Level","Level","レベル","等级","레벨"},
                new[] {"Gil","Gil","ギル","金币","길"},
                new[] {"Amount","Change","増減","变动","변동"},
                new[] {"Reason","Reason","理由","原因","이유"},
                new[] {"BalanceChange","Observed balance change","観測した残高変動","观测余额变动","관측 잔액 변동"},
                new[] {"UnknownReason","Unknown reason (not categorized as income/expense)","用途不明（収益・支出へ未分類）","原因不明（未归类为收支）","이유 불명 (수입/지출 미분류)"},
                new[] {"EmptyHint","No records yet. Enable capture and log in again. Unseen containers are not empty containers.","記録はまだありません。取得を有効にして再ログインしてください。未取得の保管場所は空とは扱いません。","暂无记录。启用采集后重新登录。未观测容器不代表空容器。","기록이 없습니다. 수집을 켜고 다시 로그인하세요. 미확인 보관함은 빈 보관함이 아닙니다."},
                new[] {"Count","Rows: {0} / quantity: {1}. Saved data; see last observed time.","表示件数: {0} / 数量合計: {1}。保存済みデータです。最終確認日時を参照してください。","行数: {0} / 总数量: {1}。已保存数据，请查看最后观测时间。","행: {0} / 수량 합계: {1}. 저장 데이터이며 마지막 확인 시간을 참고하세요."},
                new[] {"HistoryLimit","Own retainer sales and confirmed trades are not decoded yet. This log contains only observed gil balance changes; gaps and transfers cannot be classified.","リテイナーの販売履歴・成立トレードの解析は未実装です。この記録は観測したギル残高差のみで、未観測期間や資金移動の理由は判定できません。","尚未实现雇员销售和已完成交易解析。这里只记录已观测金币余额变化，无法判断间隔或转账原因。","집사 판매 및 성립한 거래 해석은 미구현입니다. 이 기록은 관측한 길 잔액 차이만 포함하며 공백 기간과 이체 이유를 판별하지 않습니다."},
                new[] {"BrowserFailure","Could not open the default browser.","既定のブラウザを開けませんでした。","无法打开默认浏览器。","기본 브라우저를 열 수 없습니다."},
                new[] {"UpdateProblem","Could not safely prepare the update.","安全に更新を準備できませんでした。","无法安全准备更新。","업데이트를 안전하게 준비할 수 없습니다."},
                new[] {"Started","Inventory Ledger started.","Inventory Ledgerを開始しました。","Inventory Ledger已启动。","Inventory Ledger를 시작했습니다."},
                new[] {"Stopped","Inventory Ledger stopped.","Inventory Ledgerを停止しました。","Inventory Ledger已停止。","Inventory Ledger를 중지했습니다."}
            };
            var languages = new[] { English, Japanese, Chinese, Korean };
            foreach (var row in rows) for (var i=0;i<languages.Length;i++) R[languages[i]][row[0]]=row[i+1];
        }
    }
}
