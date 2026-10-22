# UI, управление и надёжность взаимодействий

Аудит 22.10.2026. Исходный checkout: `dev`, `28e7da0c682b294af389ba79e30213579501630a`; проверенные исправления и регрессии сохранены в локальном commit **`dd79224`**. Unity **6000.6.0f1**, URP **17.6.0**, Input System **1.20.1**, uGUI **2.6.0**, UTF **1.8.0**. Источники: ProjectVersion, manifest/lock, настройки, исходники и реально подключённый Editor. Версии и сцены не мигрировались.

## A. Вердикт и границы

Проверен текущий город и отдельный FIRST DEAL: исходники, runtime UI, настоящие Input System events, uGUI input module, два маршрута FIRST DEAL и два городских торговых цикла. Найдены конфликты модального ввода, включая скрытый сброс сессии и действие в мире при закрытии карты. Они воспроизведены до изменений; исправления и независимые результаты RED/GREEN приведены ниже.

Автоматизированный прогон не является человеческой оценкой понятности, интереса или принятым балансом. **E6.6 остаётся открытой.** Старые отдельные UI fixtures не приравниваются к основному городу. Проверенные разрешения — диагностическая выборка, не обещание поддержки всех устройств.

Полные локальные доказательства — `Temp/UIAudit/` и указанные `Logs/`; эти каталоги игнорируются Git. Важные пользовательские save не использовались. Исходные неотслеживаемые `Assets/_Recovery/0 (1).unity` и `.meta` сохранены без изменений.

## B. UI inventory

Сокращения источников в таблице: **session** — [WorkshopSession](../Assets/Scripts/Inventory/WorkshopSession.cs) с partial Trading/Outcome/Carrying; **save** — [CitySaveController](../Assets/Scripts/Player/CitySaveController.cs). «Проверено» означает конкретный уровень, а не наличие объекта в сцене.

| UI Element | Implementation / создание | Data Source | Input / открытие и закрытие | Tested | Issues |
| --- | --- | --- | --- | --- | --- |
| City startup / pause / recovery | `CityGreyboxWalk`, `CityPauseUI`; runtime `City trading UI/City pause menu` | save, session | Esc; кнопки; F5/F9/B/N/Enter только в паузе | UTF, Player baseline lifecycle | UI-01; assertion старого T03 отдельно |
| City HUD: кошелёк, часы, активные заказы | `CityTradeView.Refresh`, `CityTradeUI.BuildUI` | session.Balance/ElapsedSeconds/Order | автоматически, скрывается в модальном UI/машине | UTF layout, E6 integration | нет подтверждённого рассинхрона в проверенных маршрутах |
| City карта и имущество | `CityTradeUI.RebuildMenu`, `CityTradeWayfinding.BuildLocalMap` | session + позиция игрока | Tab; Back/Esc/Tab; scroll, Aa, управление, equip | UTF 5 разрешений × 2 масштаба | UI-01/02; P2 initial scroll |
| City продавец и батареи | `CityTradeUI`, `CityTradeStyle.ProductCard`; E у runtime `CityTradePoint` Seller/Battery | session | E → Buy/Back/Esc/Tab; keyboard focus/Enter | E6; seller UTF; отдельная Battery точка — E6 | блокировки денег/партии; правила сохранены |
| City ящик / три экземпляра | `CityTradeUI` Crate; runtime лампы `CityTradeView.BuildWorld` | session ID/location/diagnosis | E → взять ящик/содержимое/экземпляр; закрытие общим путём | UTF layout, E6, physics | вместимость по текущему контракту — 3 |
| City диагностика / ремонт | `CityTradeView.WorkshopAction`, `WorkshopPrompt`, нижний context | session.OnBench/результат операции | E положить/взять, T тест, R ремонт; модального экрана нет | E6, physics | экран процесса ожидания не требуется: +240 игровых секунд мгновенно |
| City заказ A / Б, выбор предъявленных ID | `CityTradeUI` A/B, `SelectGoods`, `OrderSummary` | session.Order, Units | E → принять/отказаться/выбрать/передать; Back/Esc/Tab | E6 + UTF layout | городские сроки от принятия; RepeatB в городе отсутствует по правилам |
| City скупка | `CityTradeUI` Buyback | session, `WorkshopOutcome.SellBack` | E → выбрать/продать; Back/Esc/Tab | UTF layout; правила/отказы — существующие domain checks | город и FIRST DEAL имеют разные условия открытия |
| City сообщения / отказы / prompts | `CityTradeView` message + `CityMessage`, `FirstDealText`, `PlayerRaycast.FocusLabel` | фактический bool/out message операции и ближайший ray hit | автоматически; сообщение ограничено unscaled time | E6, T04/T05, UTF modal, physics | нет отдельного error dialog; сообщение не владеет моделью |
| Машина: дверь, HUD, скорость, пауза | `CityUtilityVehicle.OnGUI`, world TextMesh speedDisplay | vehicle body/IsDriving/paused | E вход/выход, Esc, WASD, Space, V | отдельные vehicle UI regressions; исходники | UI-03; это IMGUI + world TextMesh, не uGUI |
| FIRST DEAL introduction / pause / продавец / доска заказов | serialized `FirstDealView`, `FirstDealTradeBuilder` | WorkshopView.Session | E, Esc, arrows/Enter, кнопки | T04 A→B/B→A, U3 | фиксированные W/F/W; не городская RNG-партия |
| FIRST DEAL инвентарь / quick slots / preview | runtime `WorkshopCarryController.BuildUI` | та же session | Tab, 1–4, F/G/Q, кнопки слотов/Close | T04/T05 интеграция; исходники | не отдельный кошелёк; расширенная проверка всех pointer edges не выполнялась |
| FIRST DEAL мастерская / feedback | `WorkshopView`, `WorkshopAction`, serialized HUD/лампы | session, Changed | E; feedback coroutine | T04/T05; T03 повтор описан отдельно | «не проверен»/«дефект»/после ремонта различаются текстом и состоянием |
| FIRST DEAL итоги / завершение / abandon / restart | `FirstDealView`, `FirstDealText.Summary` | session accounting/orders/stock | summary/finish/confirm/restart; реальный reload | T05, U3; отдельный Player baseline | добровольный финал; Esc не возобновляет завершённую смену |
| Graybox / legacy inventory / interaction prompt | `InventoryManager`, `InventorySlot`, `InteractionPrompt`, `GrayboxInteractable`; SampleScene/FirstDealGraybox | legacy items/slots, raycast | Tab, кнопки слотов, E | T02, physics; SampleScene отдельно не проходилась | не источник городских фонарей/денег |
| ArtDirectionTest HUD | serialized `ArtDirectionTestView`, `ArtDirectionTestBuilder` | локальная WorkshopSession | F/Q/E; safe-area, prompt | исходники; свежий отдельный Play Mode не выполнялся | художественный fixture, не production menu |
| DevelopmentSandbox controls | `DevelopmentSandbox.OnGUI`, F2, runtime scene objects | существующие deal/vehicle/session | F2, teleport/clock/restart/close | исходники; свежий отдельный Play Mode не выполнялся | отладочный интерфейс, не экран игрока города |
| Main menu, полный Settings, loading, отдельные finance/history/growth | NOT IMPLEMENTED | — | — | N/A текущему циклу | не объявлены дефектами; в городе есть Aa 100/150% |
| City results / общий конец смены / Repeat B | NOT APPLICABLE | — | — | сверка CITY_GAMEPLAY | текущий город не имеет общей смены |

UI prefab-фреймворка не обнаружено: основные городские панели строятся кодом, FIRST DEAL использует существующие serialized объекты сцены. Само наличие uGUI/TMP/UI Toolkit в пакетах не доказывает использование TMP/UIDocument. Runtime game UI использует **UnityEngine.UI.Text** и встроенный **LegacyRuntime.ttf**, не TMP. Образцы Art Direction с Liberation Sans не заменяют фактическую конфигурацию игры.

## C. Архитектура и источники данных

`Input → view/adapter → валидируемая операция session → Changed → HUD/мир/открытое меню`. `TransactionCompleted → RequestSave`; запись save выполняет save controller. UI передаёт ID и команды; суммы выплат, владение и статусы меняет модель. `selected[3]`, message и focus — локальное состояние представления, не копия экономики.

`CityTradeView.Execute` временно снимает domain pause для одной операции и восстанавливает её в finally. `executingCommand` уже существовал до аудита: события обновляют мир/HUD, меню перестраивается один раз после команды. Нового MVC/MVVM, manager/EventBus или зависимости нет.

На load/New Game: `SessionReplaced → Bind`, отписка от старого Changed, закрытие старой панели, привязка новой session. `OnDestroy` снимает input/save/model callbacks и уничтожает runtime world/Canvas/созданный EventSystem/artwork. В FIRST DEAL перезапуск действительно перезагружает fixture; WorkshopView подписывается/отписывается в OnEnable/OnDisable. Автомобиль сохраняет отдельный контекст ввода, а не второй кошелёк.

## D–E. Ввод, фокус и конфликты

- `Player.PlayerInput` создаёт собственную runtime action map `Prototype Player`; `IA_PlayerInput.inputactions` не определяет актуальные E/Tab этого компонента. Active Input Handling = 1. World E остаётся в LateUpdate после камеры.
- City BuildUI создаёт EventSystem + InputSystemUIInputModule, только если текущего нет; наблюдался один EventSystem. uGUI модуль обеспечивает pointer, scroll, arrows/submit. Клавиатурная modal navigation и реальные pointer down/up проверены; drag/drop не реализован. Полная поддержка геймпада не заявляется результатом аудита.
- Модальные окна останавливают session time, движение, обзор; курсор видим и unlocked. `CanInteract` блокирует world raycast. Gameplay actions остаются enabled, но их потребители проверяют контекст; отсутствие полного переключения action maps само по себе не дефект.
- Escape закрывает текущую торговлю, затем следующим нажатием открывает паузу. В confirmation первый Esc отменяет confirmation и оставляет паузу. Tab не открывает карту поверх торгового окна, а закрывает его. В FIRST DEAL инвентарь скрывается при открытии основного меню.
- Snapshot доступности E при dispatch предотвращает отложенную world-команду после Escape. Tab потребляет дискретные gameplay callbacks этого кадра. Для отдельной автомобильной map нужна отдельная проверка приоритета перехода.
- Потеря/возврат фокуса проверялись synthetic Unity callbacks; возврат требует явного resume. **Нового настоящего OS Alt+Tab/minimize теста нет.** Исторические lifecycle результаты не выдаются за новый OS прогон.
- Отдельная mouse-delta проверка с включённым MouseInputEnabled: событие (125,75) действительно получено Input System, но в открытой карте yaw остался 180°, pitch 0°, CanInteract=false. Evidence: `Temp/UIAudit/modal-look-observation.json`.

## F–G. FIRST DEAL и синхронизация

Свежие T04: введение $300 → предложения → покупка $180 → отдельные ID → тест → батарея $50/ремонт/повторный тест → A/B → деньги/остаток → pause/resume и физический маршрут. Оба порядка основных продаж проверены отдельно; финальные $455 и отсутствие повторной оплаты проверяет harness. T05 проверяет выкуп, RepeatB, отсутствие автофинала, итоги, abandon, restart, свет/дефект/повторный тест. U3 проверяет keyboard focus, возврат после фонового клика, размеры, confirmation и restart.

Городской E6 проверяет обе партии, новые ID, повторные A/Б, старые/проданные ID, отказы, сохранение/загрузку, скрытие проданных и появление новых визуальных экземпляров. Статусы Completed/Failed/Unavailable, таймер и wallet читаются из той же session. В модальном меню deadline по утверждённому контракту не истекает; ремонт изменяет время и может изменить доступность заказа. «Дождаться expiry с открытым UI» поэтому не является ожидаемым сценарием города.

Результат операции отображается после её выполнения. Success/Failure не определяются только цветом; используются текст диагноза/статуса/причины. Автотесты повторной передачи и stale callback не выявили двойной выплаты в проверенных маршрутах. Нет утверждения, что проверены любые возможные пользовательские последовательности.

## H. Layout и читаемость

City Game View: **1280×720, 1920×1080, 2560×1440, 1920×1200, 3440×1440**, каждый при **100% и 150%**. Проверены продавец, A/Б, скупка, ящик, карта с развёрнутым управлением, пауза. Тест проверяет фактический Screen size, preferred text height, live focus, курсор; scroll clipping не считается автоматически дефектом. CanvasScaler = 1280×720 / match 0.5, safe-area anchors обновляются при изменении Screen. FIRST DEAL U3 отдельно: 1024×768, 1280×720, 1600×900, 1920×1080, 2560×1080.

Просмотрены реальные screenshots seller при пяти размерах, map/pause 1280×720, FIRST DEAL orders/results 1024×768. Кириллица и цены на этих кадрах читаемы; отсутствующие глифы не наблюдались. Это не полная проверка всех Unicode символов. Размер кнопок города обычно минимум 44 reference units; требование макета 48 и font family — отдельные design соответствия, не основание для редизайна во время аудита.

Подтверждённые P2-наблюдения: при 150% первый focus на карте прокручивает viewport к «Управление», скрывая верхние подписи мастерской/батарей; на 3440×1440/150% Back/подпись Esc магазина частично ниже viewport. Scroll позволяет добраться до них, Esc закрывает экран. Screenshot text-fit assertions этого не выявляют: видимость относительно маски — отдельный критерий. В первом проходе приоритет отдан конфликтам ввода; scroll/focus UX записан в очередь.

Изменение F5/F9: подсказка карты теперь явно говорит «в паузе», в соответствии с CITY_GAMEPLAY и исправленным приоритетом ввода. Расширенное управление включено в layout regression.

## I. Производительность

Изучены Changed/clock refresh, `RebuildMenu`, deferred Destroy, LayoutGroup/ContentSizeFitter, ForceUpdateCanvases, генерация artwork и teardown. Закрытые trade panels не перестраиваются на каждый tick; открытие и команды пересоздают виджеты. Не каждый Canvas rebuild является дефектом. Artwork создаётся при startup и переиспользуется. Городской HUD/world refresh по секундному Changed остаётся потенциальным источником allocations, но не объявлен доказанным performance blocker.

Свежий `PerformanceVerification.Run("Temp/UIAudit/commands")`: **PASS 144** внутренних проверок, один прогревочный и восемь измеренных циклов purchase → refused purchase → battery, всего **24 samples**. Медианы соответственно **6,10 / 6,09 / 6,83 мс**, максимум покупки около **13,53 мс**. Menu versions ≤2 (исходный и один новый набор controls): сохранено coalescing после model events. Это синхронное время команды в Editor, не Player frame time и не стоимость одного только UI. Полные samples — `commands/commands.json`; failure.txt отсутствует.

[Предыдущий performance audit](PERFORMANCE.md) содержит отдельный before/after и offscreen Player профиль; его цифры не являются свежим измерением этого изменения. Performance fixes в этой работе не выполнялись; ускорение не заявляется. GPU, длительный soak и низкий FPS полного UI отдельно не проверены.

Короткий текущий Editor профиль, 1600×900: отдельно стационарный HUD и открытая карта, **60 warmup + 120 frames** на состояние. ProfilerRecorder для `GC Allocated In Frame` и `Canvas.BuildBatch` валиден. Полные данные — `editor-steady-profile.json`.

| Состояние | Frame p50 / p95 / max, мс | GC mean / max, B/frame | Canvas.BuildBatch mean / max, нс |
| --- | --- | --- | --- |
| HUD | 7,66 / 8,78 / 10,19 | 12066 / 26291 | 104 / 12500 |
| Map | 6,90 / 7,78 / 8,52 | 12057 / 26252 | 0 / 0 |

Это **глобальные Editor counters**, включая сам Editor, MCP и диагностический sampler; GC нельзя приписать только игровому UI или сравнить с Player budget E5. Нули Canvas означают отсутствие rebuild samples на короткой неподвижной станции, не бесплатность открытия/scroll/транзакций. Устойчивого rebuild storm в этой выборке не наблюдалось. Before/after отсутствует, performance fixes не делались. Масштаб/размер Game View восстановлены после выборки.

## J–K. Дефекты, root cause и исправления

| ID / severity | Reproduction / expected / actual до исправления | Root cause / affected files | Решение / критерий проверки / риск |
| --- | --- | --- | --- |
| UI-01 **P1** скрытая новая игра | City → Tab → N → Enter. Ожидается ввод только карты; фактически N вооружает невидимое подтверждение, Enter заменяет session и сохраняет сброс | `CityGreyboxWalk.Update`: `_paused` true как для pause, так и для торговли, без проверки MenuOpen | Игнорировать pause shortcuts под торговлей; N/confirm в видимой паузе сохранить. RED N + raw session hashes до/после; UTF Shortcuts проверяет N/F5/F9/B, отсутствие записи и замены. Риск: устаревшая подсказка — исправлена |
| UI-02 **P1** E сквозь закрывающийся UI | Карта перед GrayboxInteractable → Tab+E. Ожидается только закрытие; фактически InteractionCount=1. Escape+E имеет тот же отложенный контекст | `PlayerInput.Update`: Tab сначала включает мир, затем dispatch E; `PlayerRaycast` переносит запрос в LateUpdate без исходного контекста | Tab потребляет дискретные команды; E принимается только из CanInteract при dispatch, ray остаётся после камеры. UTF Transitions: Tab/Esc+E, свежий E и удержание. Риск: подавляется одновременная команда с переключением меню — это намеренный приоритет |
| UI-03 **P1** отдельная map машины | У двери → карта → Tab+E: игрок сразу за рулём. За рулём на паузе → Escape+E: вместо одного resume игрок выходит из машины | `CityUtilityVehicle.Update` читает независимую action map и не потребляет переход UI | Не входить при Tab/Escape в этом кадре; после автомобильного Escape завершать обработку остальных commands. Оба отдельных теста RED; GREEN проверяет переходы и нормальные вход/выход отдельным E. Риск ограничен намеренным приоритетом menu transition |
| UI-04 **P2**, очередь | Map150 сначала показывает не верх карты; Back магазина на ultrawide150 частично скрыт | initial focus/KeepMenuFocusVisible и scroll viewport | Проверить initial focus + обнаружимость прокрутки без изменения карты/художественного направления. Не P0/P1: scroll и Esc доступны |
| QA-01 **P2**, тестовый инструмент | T03 первый запуск не получил E; повтор прошёл, но при выходе оба раза InputManager.ApplySettings assertion | Старый harness заменяет/восстанавливает transient InputSettings. Причина утраты settings при teardown требует отдельного расследования | Не менять gameplay под нестабильный harness. Сохранены fail и retry; не засчитывать как чистый PASS без оговорки. Другие UTF/T04/E6 используют существующий объект настроек |

Изменения не меняют цены, deadlines, RNG, ремонт, правила владения, save schema, scene/prefab GUID, art direction, Unity или packages. Исправления ограничены приоритетом ввода и соответствующей подсказкой.

## L. Проверки и evidence

| Проверка | Результат | Полный локальный результат |
| --- | --- | --- |
| Исходные UTF regressions | **RED 2/2** по ожидаемым причинам; первый прогон также выявил пустой scene path в test teardown, обработка исправлена | `Temp/UIAudit/red-results.xml`, `before-reset.txt`, `before-tab-e.txt` |
| Shortcuts после первой правки | **1/1 PASS**, 14 внутренних утверждений | `shortcuts-fixed-results.xml` |
| City UI, первый GREEN | **3/3 PASS**, позднее добавлено развёрнутое управление и vehicle cases | `ui-results-1.xml`, `ui-final-results.xml`; каталоги Shortcuts/Transitions/Screens |
| Vehicle regressions до отдельной правки | **RED 2/2**: вход через карту и выход при resume | `vehicle-red-results.xml`, `vehicle-transition-observation.txt` |
| Итоговый общий UTF прогон | **6/6 PASS, 0 failed**: 5 UI + 1 physics; UI внутренние counts: Screens 1368, Shortcuts 14, Transitions 6, VehicleEntry 7, VehiclePause 3 | `all-final-results.xml`; последние каталоги сценариев в `Temp/UIAudit/` |
| PhysicsInteractionTests | **1/1 PASS**, реальный Play Mode; сохранён same-frame camera E и ray blocking | `physics-results.xml`; `Temp/PhysicsAudit/NUnit-*/checks.log` |
| FIRST DEAL T04 A→B / B→A | **PASS / PASS**, по 107 PASS-строк в каждом логе | `first-deal-A-then-B.log`, `first-deal-B-then-A.log` |
| FIRST DEAL T05 outcomes | **PASS**, 88 PASS-строк | `first-deal-outcomes.log` |
| FIRST DEAL U3 focus/sizes | **PASS 2266** внутренних утверждений | `first-deal-focus.log`, `Logs/U3-*.png` |
| City E6 domain / integration | **PASS 8651 / 266** внутренних утверждений | `city-domain.log`, `city-second-cycle.log` |
| T02 legacy pickup/inventory/input | **PASS**, 23 PASS-строки | `legacy-input.log` |
| T03 workshop | **FAIL первого запуска; PASS 51 при повторе с teardown assertion**, QA-01 | `workshop-initial-failure.log`, `workshop-retry.log`; InputManager.cs:2997 assertion |
| Windows64 city Player baseline | **exit 0, PASS 259**; 3 NewGame/load cycles, reload, corrupt save/recovery, E/Esc | `player-baseline/lifecycle-report.json`, `player.log` |
| Windows64 FIRST DEAL Player baseline | **exit 0, PASS 105**, 3 sessions/restarts, A→B/B→A и abandon | `first-deal-player-baseline/first-deal-report.json`, `player.log` |
| Command menu experiment | **PASS 144**, 24 measured samples | `commands/commands.json` |

NUnit tests и внутренние assertions разных harness нельзя складывать в «число тестов». MCP `run_tests` job после Enter/ExitPlayMode оставался stale/running, хотя UTF уже сохранил XML. Последующие прогоны выполнены через штатный `TestRunnerApi.Execute`; итог взят из фактического TestResults.xml, скопированного в Temp/UIAudit. Первый discovery до AssetDatabase.Refresh нашёл 0 тестов и **не засчитан** как проверка.

Промежуточный общий прогон был **5/6**: VehiclePause потерял самый первый synthetic E ещё на подготовке входа (в diagnostic log E=False, CanInteract=True). Устройство теперь получает нейтральный state и два update до первого edge; это изменение только fixture. Изолированный повтор — **1/1**, затем весь набор — **6/6**. Исходные fail checks сохранены в каталогах `VehiclePause-aa3f...` / `VehiclePause-2a1d...`, успешный отдельный XML — `vehicle-pause-stabilized.xml`. Не выдавать потерю первого события test setup за ещё одно исправление gameplay.

Player использовал существующие `Builds/PhysicsAudit/HustleScale.exe` и `Builds/LifecycleAudit/FirstDeal/HustleScale.exe`, без новых сборок/архивов. Они предшествуют правкам этого аудита: свежие source fixes подтверждены Editor, **не этими Player binaries**. Оба Player запускались с batchmode и графическим backend, изолированными save; это automated smoke, не видимый ручной desktop walkthrough и не OS-focus тест.

Полный текущий Editor log сохранён в `Temp/UIAudit/editor-full.log`. В конце Play Mode остановлен, открыта чистая `CityGreybox`, Game View size восстановлен; runtime background/input overrides возвращены к конфигурации проекта, save override — к исходному значению.

## M. Negative matrix и оставшиеся ограничения

| Сценарии запроса | Свежая проверка / предел |
| --- | --- |
| 1–2, 4–6: open/close, Esc, hold, transition, несколько окон | UTF Transitions/Shortcuts/Vehicle; physics; U3 |
| 3: двойное подтверждение | T04/T05 stale callbacks, E6 повтор команды; отдельные два физических mouse click по каждому торговому действию не проверены |
| 7: уйти без покупки | T04/U3/T05, изменение денег не происходит |
| 8–9: focus/Alt+Tab | synthetic callbacks — PASS; новый настоящий OS Alt+Tab — NOT TESTED |
| 10–11: resolution, inventory after purchase | UTF sizes, T04/E6; снимки и state checks |
| 12: заказ при другом UI | world commands блокируются модальным контекстом; напрямую чужое окно команду не получает |
| 13–14: нехватка денег / неправильный товар | T04/T05/E6 domain и integration; полный pointer failure matrix каждого окна не заявляется |
| 15: deadline в открытом меню | NOT APPLICABLE: меню замораживает время; expiry в активном мире/domain проверен существующими маршрутами |
| 16: тест → изменение | T04/T05/E6 ремонт сбрасывает diagnosis, ретест обязателен |
| 17–18, 24: restart, повторный заказ, повторные sessions | U3/T05/E6; 3 sessions каждого Player baseline; свежий длительный memory soak не выполнялся |
| 19: огромные деньги | за пределами естественных сумм текущих двух партий; полный int.MaxValue layout — NOT TESTED |
| 20: ёмкость | 3 фиксированных экземпляра/слота проверены; не заявлена бесконечная inventory list |
| 21: низкий FPS UI | NOT TESTED полным UI-прогоном; physics sampling не заменяет этот пункт |
| 22: длинные русские строки | layout матрица + expanded controls/U3; произвольная локализация — NOT TESTED |
| 23: уничтожение цели/controls | physics и rebuild/focus checks; каждый возможный Destroy callback race не исчерпан |

Следующий приоритет: UI-04 scroll/focus, QA-01 harness teardown, затем отдельно новый Player с этими исходниками и реальный OS focus/input smoke, после — человеческая E6.6. Для gamepad, полноценного settings, drag/drop, отдельного loading не создавались новые механики. Человеческая приёмка и performance на минимальном hardware остаются отдельными задачами.

## N. Git и завершение

Сценовые/prefab `.meta` и ссылки не изменены. Новые `.meta` созданы Unity для двух файлов проверки. Код и тесты: `dd79224` — `fix(input): isolate modal shortcuts from gameplay actions`. Compilation и тесты выше наблюдались до commit; scoped `dotnet format whitespace --verify-no-changes`, проверка новых GUID/Markdown file links, focused diff review и `git diff --check` прошли. Hook дополнительно проверил форматирование семи C# и двух `.meta`; незакоммиченных правок кода после него нет. Документация оформлена отдельным локальным commit. Исходная recovery-пара остаётся untracked. Push, reset, смены ветки, новые Player candidates и ZIP не выполнялись.
