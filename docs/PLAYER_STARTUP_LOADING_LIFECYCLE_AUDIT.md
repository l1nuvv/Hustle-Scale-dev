# Windows Player: запуск, загрузка и lifecycle — 22.10.2026

## A. Результат и границы

Настоящие Windows x64 Player собраны и запущены напрямую. Финальный обычный Player трижды запущен из полной копии вне проекта, с закрытым Editor: **READY, exit 0, runtime errors 0**. Отдельно проверены городской маршрут с сохранением между процессами, повторные New Game/load, реальные перезагрузки сцен, FIRST DEAL fixture, Alt+Tab, сворачивание/восстановление, X и Alt+F4.

Подтверждённых P0/P1 дефектов игры на этих маршрутах не найдено. Исправлен устаревший Player harness; добавлены ограниченная диагностика и целевые регрессии. Игровые правила, качество изображения, packages, Unity и build settings не менялись. Ускорение загрузки не заявляется. Это техническая проверка, **не** человеческая приёмка E6.6 и не доказательство отсутствия ошибок во всех сценариях.

Важное различие: production Player входит в **CityGreybox**, автоматически создаёт/загружает город и показывает паузу. Главное меню, Return to Menu и New Game → FIRST DEAL не реализованы. FIRST DEAL проверен отдельной fixture-сборкой с единственной сценой FirstDealTrade; она не добавлена в production build settings.

## B. Checkout, окружение и сборки

| Параметр | Факт |
| --- | --- |
| Начало аудита | Ветка `dev`, чистое дерево, HEAD `65ef835086e582a74c6b7b66835f67fabcbced76` |
| Проверенный код | `6111e0f46af9357a3bb1588d62fa75605276abfc`; сборки сделаны с соответствующими изменениями до локального коммита |
| Unity | 6000.6.0f1, revision f7f8ed4d1e24 |
| ОС / CPU / память | Windows 11; Ryzen 7 3700X, 8 cores; Unity SystemInfo: 32683 MB RAM |
| GPU / API | RTX 4060 Ti, 7949 MB VRAM; Direct3D11. Настройки допускают D3D11/D3D12, автоматический выбор списка выключен |
| Backend / stripping | Live Editor API: Mono2x / Disabled |
| API profile | Serialized `apiCompatibilityLevel = 6`; live API enum `NET_Standard_2_0`. Это имя enum Unity; IDE target v4.7.1 не является Player profile |
| Render pipeline | URP 17.6.0, `PC_RPAsset`; Input System 1.20.1, uGUI 2.6.0, Test Framework 1.8.0 |
| Качество | **PC**, render scale 1, HDR, MSAA 1, vSync 0, targetFrameRate −1, 1600×900 windowed |
| Индекс качества | Editor index 1; Player index 0, **название PC**. Mobile исключён из Standalone, индексы перенумерованы; это не переключение на Mobile |
| Production сцены | CityGreybox включена; FirstDealTrade отключена. Активного Build Profile нет |
| Defines / плагины | Standalone `UNITY_POST_PROCESSING_STACK_V2`; собственных native DLL/asmdef под Assets не найдено. Addressables/AssetBundles в маршруте не используются |
| Профилирование | Обычные Release-запуски без profiler/deep profiling/autoconnect. Один отдельный Development CPU capture; его времена не смешиваются с Release |

Manifest/lock прочитаны, включая Editor MCP, Rider, AI Navigation, Timeline и Visual Scripting. Наличие пакета не означает использование игровой системой. Прочитаны действующие AGENTS, README, ROADMAP, DEVELOPMENT, ENGINEERING_STANDARD, ARCHITECTURE, GDD, CITY_GAMEPLAY, FIRST_DEAL, PLAYTEST и предыдущие аудиты производительности/состояния.

| Сборка / локальный output | Результат | Время сборки / warnings |
| --- | --- | --- |
| Исходная Development, исходный HEAD | Succeeded, 0 errors | 21,23 с / 3 |
| Исходная обычная, исходный HEAD | Succeeded, 0 errors | 22,68 с / 3 |
| `Builds/LifecycleAudit/Development` | Succeeded, 0 errors | 27,63 с / 3 |
| `Builds/LifecycleAudit/Release` | Succeeded, 0 errors | 20,89 с / 3 |
| `Builds/LifecycleAudit/FirstDeal` | Succeeded, 0 errors | 26,83 с / 3 |
| `Builds/LifecycleAudit/DevelopmentVerified` | Succeeded, 0 errors | 23,39 с / 3 |
| **`Builds/LifecycleAudit/ReleaseVerified`** | **Succeeded, 0 errors, 112253660 bytes** | **17,32 с / 3** |

Три штатных build warnings: нет RuntimePipelineConfig у `com.unity.pipeline` (этот пакет отключает свой runtime, **URP продолжает работать**); два CS0618 старого FindObjects API в CityTradeWayfinding. Не исправлялись вне области задачи.

Сохранена также история неуспешных промежуточных действий: первая компиляция новой диагностики до AssetDatabase refresh получила CS0103; после полного импорта компиляция прошла. Две промежуточные сборки дали дополнительное предупреждение о pending code changes. Финальные синхронные сборки после завершения компиляции имеют только три исходных warnings. Потерянный при domain reload MCP job не трактовался как успех: итог проверен через BuildReport и реальный Player.

Исходные baseline outputs/логи были в Temp, которую очистил перезапуск Editor. Их результаты выше зафиксированы по ответам инструментов, исходные полные baseline Player.log больше недоступны. Все последующие доказательства находятся в **Logs/LifecycleAudit**, сборки — в **Builds/LifecycleAudit**; обе папки игнорируются Git. Это ограничение доказательств, а не новый повтор baseline.

### Воспроизведение сборки и запуска

Использован существующий Unity BuildPipeline через подключённый Editor. Для production — `BuildPlayerOptions`: scenes = `Assets/Scenes/CityGreybox.unity`, target = `StandaloneWindows64`, options = `DetailedBuildReport`; для Development дополнительно `Development`. Для fixture scenes заменяется только в аргументе сборки на `Assets/Scenes/FirstDealTrade.unity`. Постоянный список сцен не изменяется. Новая система сборки, архивы и release packaging не создавались.

Пример обычного диагностического запуска из PowerShell; для следующего fresh-run выбрать новую папку:

```powershell
$auditRoot = 'E:\Code\Hustle_Scale\Hustle-Scale_dev'
$auditOut = Join-Path $auditRoot 'Logs/LifecycleAudit/new-startup'
New-Item -ItemType Directory -Path $auditOut -Force | Out-Null
& "$auditRoot/Builds/LifecycleAudit/ReleaseVerified/HustleScale.exe" `
  -screen-width 1600 -screen-height 900 -screen-fullscreen 0 `
  "-lifecycle-audit=$auditOut" -logFile "$auditOut/player.log"
```

Флаг `-lifecycle-audit=<absolute-directory>` создаёт наблюдатель и **отдельный** city-save.json рядом с результатом. Он не меняет качество, background mode, паузу или ввод; завершает процесс через 5 секунд после готовности, либо через 20 секунд без готовности, если Player loop выполняется. `-lifecycle-manual` отключает автоматический выход для GUI-проверки; приложение нужно закрыть. Без audit-флага наблюдатель и его файлы не создаются. Не объединять audit, city-verify и e5-profile: это разные режимы с разными save overrides.

Для Development harness используется `-batchmode -city-verify=<absolute-directory> -city-verify-phase=lifecycle`. Для межпроцессного маршрута — последовательно `prepare`, `continue`, `restore` в одной папке; после prepare скопировать city-save.json в prepare-save.json, после continue — в continue-save.json. Prepare требует пустой тестовой папки. FIRST DEAL fixture: `-batchmode -first-deal-verify=<absolute-directory>`. Для logFile заранее создать папку. Никакие пользовательские saves этими режимами не используются.

## C. Startup timeline

Основной срез — точный финальный Release, build GUID **e4c3480228d54b33afff4b1083d023d0**, три запуска из `C:\Users\l1nuvv\AppData\Local\Temp\Hustle Scale Lifecycle 20261022\Проверенный Player`, cwd там же. Editor и его import workers отсутствовали; Unity Hub service не является Editor.

Внешний наблюдатель фиксировал UTC непосредственно перед Start-Process и опрашивал MainWindowHandle каждые 20 мс. Внутренние события содержат UTC и Unity runtime time. Времена ниже — от внешнего запуска, рассчитанные по UTC, не от первого Awake.

| Запуск | Первое окно, мс | Сессия и UI готовы, мс | Первый SRP context complete, мс | Exit |
| --- | ---: | ---: | ---: | ---: |
| Verified1 | 960,9 | 3978,5 | 4352,9 | 0 |
| Verified2 | 848,7 | 3918,8 | 4297,6 | 0 |
| Verified3 | 822,4 | 3860,2 | 4229,6 | 0 |

Окно ещё не означает готовность. Ready означает существование сессии, её привязку к trade и построенный UI; стартовая пауза намеренно оставляет CanInteract=false. Готовность к взаимодействию после resume проверена E → PlayerRaycast, но отдельное внешнее время до первого E не измерено. Главное меню отсутствует. SRP callback подтверждает завершение CPU-подачи render context, **не** показ первого кадра монитором. Отдельные first-present/GPU/window-compositor timings недоступны.

Пример внутренних этапов Verified1, длительности между stamps:

| Участок | мс |
| --- | ---: |
| CitySaveController.Start → готовность новой session/save | 21,95 |
| CityTradeView.Start → BuildWorld | 41,03 |
| BuildUI | 85,23 |
| Подписки и Bind после UI | 8,98 |
| Готовность UI → первый SRP context complete | 374,40 |

BeforeSplashScreen, BeforeSceneLoad, sceneLoaded, save/trade initialization, readiness и shutdown записаны в startup.json. Native runtime/graphics stages дополнительно видны в CPU capture; C# callbacks не выдаются за начало ОС-процесса.

## D. Loading performance и память

| Финальный Release | Frames | p95 интервала LateUpdate, мс | p99, мс | Max, мс | Unity allocated / managed, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| Verified1 | 2713 | 3,53 | 3,97 | 391,09 | 324,1 / 3,4 |
| Verified2 | 2566 | 3,50 | 3,99 | 400,76 | 333,2 / 3,3 |
| Verified3 | 2761 | 3,54 | 4,02 | 390,52 | 333,1 / 3,4 |

Это первые пять секунд **паузного стартового экрана**, начиная с первого LateUpdate. Весь native startup до первого LateUpdate в эти percentiles не входит; он отражён в timeline/Profiler. Наблюдатель хранит не более первых 4096 интервалов; некоторые ранние прогоны достигли лимита. Это не steady-state gameplay FPS, не GPU frame time и не длительный memory soak. Память — снимок в конце, не peak и не полный working set процесса; allocated/managed не следует складывать как независимые категории.

До финальной детализации выполнены ещё три Release и три portable запуска: ready 3,858–3,990 с, submission 4,251–4,376 с; max 384–437 мс, кроме одного **1205,21 мс** в Release2. Этот выброс не скрыт, не воспроизведён в финальной серии и не объявлен исправленным. Фоновая нагрузка рабочего компьютера не контролировалась полностью.

Cold-cache запуск не заявляется: Library, OS/GPU caches не очищались. Это первый запуск нового output и последующие последовательные запуски с неизвестным состоянием системных кешей. Загрузка купленного save и повторные сцены проверялись функционально; их p95/p99/внешняя длительность отдельно не измерены.

### CPU capture, отдельный Development Player

`Logs/LifecycleAudit/StartupProfile/startup.raw` (14,6 MB), `cpu-samples.txt`, `worker-samples.txt`. Запуск с `-profiler-enable -profiler-log-file <absolute-path> -profiler-capture-frame-count 300`, без Deep Profiling; расшифрованы frame indices 0–2. Настройка соответствует [официальным аргументам Profiler](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/profiler/command-line-arguments); чтение через [RawFrameDataView](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/profiling/rawframedataview), API проверены reflection в установленном Editor.

| Inclusive CPU sample | мс |
| --- | ---: |
| Startup Main Thread frame 0 | 3854,79 |
| Initialize Graphics | 805,80 |
| Loading Global Managers | 409,15 |
| ReloadAssembly | 201,46 |
| Application.LoadLevelAsync Integrate | 320,53 |
| InputSystem.RuntimeInitialize | 189,70 |
| Первый PlayerLoop frame 1 | 605,64 |
| CityTradeView.Start | 145,69 |
| CityTradeView.LateUpdate | 59,25 |
| PostLateUpdate.FinishFrameRendering | 327,06 |
| URP RenderSingleCameraInternal | 190,58 |

Это вложенные inclusive samples: их **нельзя суммировать**. Данные локализуют значительную часть штатного старта в graphics/runtime/input discovery и первичном построении UI/URP RenderGraph. Они не устанавливают причину отдельного Release2-выброса 1,2 с, не дают GPU/driver breakdown и не доказывают shader compilation как единственную причину. На основании этого среза не введены prewarm, новое asset loading или понижение качества.

## E. Инициализация и зависимости

1. Runtime hooks опциональных harness/наблюдателя разбирают свои явные флаги до сцены. Только эти диагностические объекты используют DontDestroyOnLoad.
2. PlayerInput.Awake создаёт собственную action map; OnEnable включает её. PlayerController.Awake получает input/camera и CharacterController.
3. CityGreyboxWalk.Awake (`DefaultExecutionOrder(100)`) добавляет CitySaveController, если он отсутствует, и CityTradeView. Динамически добавленный SaveController сразу кеширует ссылки в Awake; RestorePose выполняется позднее в Start.
4. CitySaveController.Start (`-200`) после scene Awake находит vehicle, восстанавливает валидного кандидата либо создаёт новую city session. Нет готового save — нормальная новая игра; backup-only — пауза и предложение восстановления.
5. CityTradeView.Start строит runtime мир/фонари/NPC и uGUI, подключает SessionReplaced/BeforeSave/input, затем явно Bind читает уже существующую session. Поэтому уведомление, случившееся до подписки, компенсируется начальным чтением состояния.
6. При замене SaveController снимает TransactionCompleted старой модели; trade снимает Changed, меняет session и восстанавливает world presentation. В проверке изменение старой detached модели не затрагивало новую игру.
7. OnDestroy снимает подписки, удаляет runtime root/Canvas/принадлежащий EventSystem/materials/audio; InputActionMap освобождается. Reload в Player оставляет один save/walk/trade/vehicle и один trade Canvas.

В production нет глобального bootstrap, sceneLoaded/activeSceneChanged orchestration или фоновых gameplay Task/Thread. Сессия владеет экономикой; UI не создаёт вторую копию денег. Fixture FIRST DEAL использует отдельные WorkshopView/FirstDealView и собственный scene restart. Предполагаемые проблемы произвольного Start-order не объявлены дефектами без воспроизведения.

## F. Сцены, сессии и FIRST DEAL

| Реальный Player harness | Результат / доказательства в Logs/LifecycleAudit |
| --- | --- |
| City prepare | PASS 2201 assertions, exit 0: Trading/prepare-report.json; покупка, повторный отказ, батарея, перенос, свободная постановка, диагноз, ремонт, три load |
| City continue, новый процесс | PASS 1509, exit 0: Trading/continue-report.json; точное восстановление ремонта, retest, Б затем A, баланс $455, запрет повторных выплат |
| City restore, третий процесс | PASS 101, exit 0: Trading/restore-report.json; точный snapshot, повторные отказы выплат, новая партия за $180 с новыми ID/историей, баланс $275 |
| Первые lifecycle серии | PASS 217 / 271 / 259: Lifecycle1–3; отдельно EscapeRegression PASS 281 |
| Финальные InputLifecycle1–3 | **PASS 271 / 269 / 269**, exit 0: Esc в обе стороны, E через PlayerRaycast, 3 New Game/menu/pause/load, detached model, corrupt-save отказ, backup recovery, SceneManager.LoadSceneAsync |
| FIRST DEAL fixture, три процесса | **PASS 105 каждый**, exit 0: FirstDeal1–3/first-deal-report.json; в каждом A+B и B+A с ремонтом ($455), просрочка/выкуп ($275), summary/end и три настоящих restart сцены |

Числа — **assertions**, не NUnit test cases и не участники. Frame/timeout assertions меняются со скоростью исполнения. FIRST DEAL использует реальные компоненты/кнопочные методы, часть переносов и ускоренное истечение срока выполняет вызовами модели; это интеграционный сценарий, не самостоятельная ходьба игрока. City collision route шагает капсулой, не измеряет WASD/навигацию/игровой баланс. Seed 41 используется только внутри opt-in диагностического prepare, как в существующем S2/S3; обычные две партии остаются независимыми RNG.

Trading и FirstDeal проверены до добавления последних city E/Esc checks и детализации observer; правила игры между этими срезами не менялись. InputLifecycle1–3 и ReleaseVerified относятся к окончательным исходникам коммита 6111e0f. RepeatB и все альтернативные gameplay ветви не перепроверялись в Player в этом lifecycle-аудите.

Набор project NUnit/UnityTest не добавлялся: используется существующий menu/Player harness подход. Свежий массовый Editor domain rerun не выполнялся; исторические T06/S2/E6 results остаются в [аудите состояния](GAME_STATE_RELIABILITY_AUDIT.md), здесь не выдаются за новые проверки.

## G. Application lifecycle

Обычный Release с `-lifecycle-manual`, отдельные saves в ManualUI и AltF4. После первоначального отказа Computer Use доступ стал доступен; первоначальный отказ больше не является текущим блокером.

- Увидены реальное стартовое меню паузы с $300, работа мышиной кнопки Continue и игровой HUD; подтверждение New Game возвращает чистую сессию и паузу.
- Реальный Alt+Tab → возврат показывает паузу. В startup.json записаны OS focus false/true и pause true/false. Автоматического resume нет — это действующая политика.
- Минимизация системной кнопкой подтверждена инструментом `window is minimized`; процесс отвечал. После activate/get_window — та же пауза и рабочие кнопки.
- X закрыл окно, OnApplicationQuit и Input Polling Thread shutdown записаны; PID 9108 исчез. Его внешний watcher к тому времени уже закончил 120-секундное ожидание, поэтому exit code для **этого X-прогона не получен**.
- Новый процесс PID 16512 закрыт реальным Alt+F4: внешний watcher подтвердил **exit 0** (AltF4/exit.json), callback/cleanup в Player.log.
- Esc через OS-инъекцию Computer Use не дал переключения; причина не установлена. В трёх финальных Player-прогонах queued Input System Esc и E проходят. Это не основание утверждать, что физическая клавиатура полностью принята или что ошибку OS-инъекции исправили.

runInBackground=false в обычном Player; diagnostic observer его не меняет. Городская пауза блокирует session clock/movement/camera/cursor capture, без глобального изменения Time.timeScale. Vehicle имеет собственную focus/pause ветку; реальный focus test **за рулём** не проведён. Аудиоповедение при потере фокуса не оценено прослушиванием.

Editor при закрытии оставлял Mono/import-worker процессы после исчезновения окна; сохранены Editor shutdown log и cleanup-запись. Только эти известные остаточные PID очищены отдельно перед запуском без Editor. Это **Editor teardown**, не найденная утечка Player и не основание менять игровые packages. Финальные Player запускались при отсутствии Editor-процессов.

## H. Полнота и переносимость

Полная ReleaseVerified-копия: **198 файлов, 0 несовпадений SHA-256** с build output. Сохранены exe, Data, MonoBleedingEdge, UnityPlayer и остальные библиотеки. Путь с пробелами/кириллицей, cwd за пределами исходников, три успешных запуска без Editor. Сам проект не переименовывался и не удалялся; это проверка независимости от cwd/упаковки, не доказательство запуска на чистом другом компьютере без доступа к диску E:.

Runtime поиск не выявил чтения исходных Assets, абсолютных developer paths или неограниченного UnityEditor API. Editor обращения в CitySaveController/DevelopmentSandbox находятся под UNITY_EDITOR; production saves используют persistentDataPath. Прямые serialized prefab/material/scene references и builtin LegacyRuntime font вошли в Player и реально использовались. Строки путей в Development stack traces — debug symbols, не доказательство runtime загрузки из проекта.

Player.log содержит четыре сообщения о stripped/unsupported Gaussian/Bokeh DOF и Panini shaders. Они не попали в managed warning/error counts и сохранены отдельно как сообщения журнала. В используемом DefaultVolumeProfile DOF mode=Off; URP stripping unused post-processing включён. Внешний вид показанного города не доказал потери обязательного эффекта; отсутствие всех возможных визуальных regressions не утверждается. Глобально выключать stripping ради этих сообщений не стали.

Пользовательские city-save.json и backup скопированы перед GUI-запусками; повторная резервная копия и итоговая проверка находятся в Logs/LifecycleAudit/UserSaveBackup и user-save-final-verification.json. Оба SHA-256 остались прежними; время записи исходных файлов — 20.10.2026. Служебные новые saves, повреждения и .tmp создавались только в диагностических папках.

## I. Findings и root cause

| ID / priority | Факт, воспроизведение, причина | Действие / критерий / риск |
| --- | --- | --- |
| PL-01 / P2, подтверждён и исправлен | На исходном HEAD Development `-city-verify-phase=prepare`: exit 4, 2135 assertions, InvalidOperationException / Sequence contains no matching element в Point(WorkshopDock), после покупки и маршрута к мастерской. Harness ожидал удалённый dock и модальное bench/test/repair, старый collider/dock state, фиксированную городскую партию и запрет второго лота | CityPlayerVerification обновлён под прямую мастерскую, DropHeld/HasCratePose, Bench collider, seed 41 внутри fixture, разрешённый второй лот. Критерий: prepare/continue/restore с точными snapshots/платежами — PASS. Риск: reflection seed связан с именем приватного поля, но используется только в тесте |
| PL-02 / P2, измеренное ограничение | Стартовые интервалы ~390–401 мс в финальной серии. От native graphics/runtime до первой подачи рендера несколько секунд; CPU trace локализует runtime/input/URP/UI work | Добавлена лёгкая opt-in временная шкала. Оптимизация не реализована; критерий будущей работы — согласованный startup budget и сопоставимый повтор CPU/GPU capture. Риск необоснованной оптимизации выше доказанного выигрыша |
| PL-03 / P2, не локализован | Разовый max 1205,21 мс в Release2; в финальной серии не повторился. Причина и точный sample этого выброса не установлены | Оставлен открытым наблюдением, не объявлен исправленным. При повторе — capture на том же workload; не связывать автоматически с shader compilation |
| PL-04 / P3, сообщения подтверждены, visual defect не подтверждён | Четыре post-processing stripped/unsupported shader сообщения при включённом stripping и DOF Off | Сохранены логи; настройки не менялись. Проверять обязательность эффекта/визуальный before-after прежде изменения build resources |
| PL-05 / открытая гипотеза ввода | Синтетический OS Esc не переключил паузу; queued Input System Esc проходит в Player | Не исправлялся код игры без root cause. Следующая проверка — физическая клавиатура и raw input trace. Не считать это воспроизводимой необратимой потерей управления |

Affected source PL-01: CityPlayerVerification.cs. PL-02/03 затрагивают startup сцены, CitySaveController/CityTradeView и engine rendering; это локализация измерений, не доказательство дефекта каждого файла. PL-04 относится к URP settings/runtime shader resources; PL-05 — к цепочке OS injection → Input System → CityGreyboxWalk/PlayerController.

## J. Реализованные изменения

- **CityStartupDiagnostics.cs + meta:** явный флаг, абсолютный isolated save, BeforeSplash/BeforeScene/sceneLoaded/stage UTC, ready/render submission, ограниченные frame samples, memory snapshot, focus/pause/quit events; нет profiler/background/quality overrides.
- **CitySaveController.cs:** только диагностические stamps и выбор отдельного save по явному audit-флагу.
- **CityTradeView.cs:** только stamps вокруг Start/BuildWorld/BuildUI/Bind; экономические команды не менялись.
- **CityPlayerVerification.cs:** исправлен PL-01; lifecycle/input/negative-load/reload regressions и dispatch отдельного fixture.
- **FirstDealPlayerVerification.cs + meta:** расширение существующего opt-in Development harness для трёх fixture маршрутов и scene restart.

Высокоприоритетных runtime дефектов с установленной причиной не было, поэтому gameplay fixes не выдумывались. Новый bootstrap/scene manager/save framework, пакеты и баланс не добавлялись.

## K. Before / after

| Что | До | После |
| --- | --- | --- |
| City Player harness | Реальный FAIL у удалённого WorkshopDock | prepare/continue/restore PASS; повторный лот и сохранённая история |
| Startup evidence | Нет отделённых process/window/ready/render-submit метрик | 9 измеренных Release запусков, в том числе 3 точного финального output; UTC + внешние PID/exit/окно |
| Lifecycle coverage | Устаревший трёхпроцессный маршрут | Дополнительно Input System E/Esc, 3× New Game/load/pause/reload, corrupt-save recovery, FIRST DEAL fixture и реальная GUI-проверка OS lifecycle |
| Скорость игры / загрузки | Не оптимизировалась в этой задаче | Улучшение не заявляется; различия таблиц не являются эффектом исправления harness |

## L. Acceptance matrix

| Сценарий | Статус | Фактическое доказательство / граница |
| --- | --- | --- |
| Первый прямой запуск exe | PASS | Реальные Development/Release output, READY, окно, exit 0 |
| Повторные запуски | PASS | Verified1–3, один build GUID, отдельные процессы |
| Рабочее главное меню | NOT IMPLEMENTED | Вместо него стартовая city pause UI |
| Стартовая city pause UI | PASS | Видимое окно, Continue мышью, New Game confirmation |
| New Game → FIRST DEAL | NOT IMPLEMENTED | Production входит в CityGreybox |
| New Game города | PASS | GUI reset и InputLifecycle1–3, $300 / 0 времени / новые session |
| Первое взаимодействие | PASS | Queued E → InputAction → PlayerRaycast → seller menu; навигация человека отдельно |
| Continue / Load | PASS | Автозагрузка между процессами, повторный явный load, exact snapshot |
| Return to Menu | NOT IMPLEMENTED | Нет такого пользовательского перехода |
| New Game again / отсутствие дублей | PASS | 3 цикла на процесс, detached model и reload component counts |
| FIRST DEAL Restart | PASS | FirstDeal1–3, по три реальных reload и свежие $300 |
| Alt+Tab | PASS | Реальный OS input, focus events, возврат в паузу, Continue |
| Minimize / Restore | PASS | Подтверждение minimized, responding PID, восстановленный UI |
| Pause / Resume | PASS | GUI Continue, queued Esc, неизменный clock на паузе |
| Физические Esc/WASD/мышь | NOT TESTED | OS injection Esc неоднозначен; это не human acceptance |
| X закрывает окно | PASS | OnApplicationQuit + input shutdown + исчезновение PID; exit code недоступен |
| Alt+F4 | PASS | AltF4/exit.json: exit 0, повтор после X |
| Кнопка Quit в игре | NOT IMPLEMENTED | В city pause UI её нет |
| Standalone folder | PASS | Вне проекта, кириллица/пробелы, 198 совпадающих файлов, Editor отсутствует |
| Основные FIRST DEAL lifecycle маршруты | PASS | Отдельная fixture-сборка, сценарные API, ремонт/A/B/сроки/выкуп/end/restart; RepeatB отдельно не повторён |
| Corrupt save / recovery | PASS | Изолированное повреждение, atomic load rejection, рабочий backup |
| Отсутствующий необязательный config | NOT IMPLEMENTED | Такого runtime config в текущем маршруте нет |
| Missing required asset/scene / init failure | NOT TESTED | Production assets не удалялись; controlled missing-reference fixture не добавлялся |
| Fullscreen / resolution menu | NOT IMPLEMENTED | В игре нет такого меню; проверки выполнены в фиксированном windowed режиме |

PASS относится только к описанному уровню проверки. Статическое чтение не используется как основание PASS. E6.6 остаётся открытой человеческой приёмкой.

## M. Оставшиеся ограничения и дальнейшие задачи

1. При повторе PL-03 сохранить capture вокруг конкретного 1,2-секундного выброса; не объявлять его устранённым по трём чистым запускам.
2. Установить критерий приемлемого старта и при необходимости измерить first-present/GPU; тяжёлую оптимизацию до этого не делать.
3. Физическая клавиатура/ввод после focus за рулём, прослушивание аудио, длительный memory soak, первый запуск на другой машине и независимая E6.6 не проверены.
4. Missing required component/scene и close во время файловой замены не инъецировались. Повреждённый save, отсутствие save, backup recovery и повторные команды покрыты доступным harness.
5. Не переносить route/числа FIRST DEAL в город и не добавлять главное меню как «исправление» отсутствующей функции без отдельной задачи.

## N. Git и доказательства

Код/диагностика/регрессии — локальный коммит **6111e0f** (`test(player): verify standalone startup and session lifecycle`). Этот отчёт и ссылка из DEVELOPMENT фиксируются отдельным docs-коммитом. Ветка `dev`, push не выполнялся.

В ходе Editor recovery появились неотслеживаемые `Assets/_Recovery/0 (1).unity` и `.meta`; они оставлены отдельно и не включены в коммиты. Исходно дерево было чистым. Build output, saves, raw captures и logs не коммитятся. Diff/format checks относятся только к явным изменённым путям.

Основные локальные доказательства относительно корня репозитория:

- `Logs/LifecycleAudit/development-verified-build.log`, `release-verified-build.log`, `editor-verification.log` — итоговые BuildReport/Editor, 0 errors / 3 warnings.
- `Logs/LifecycleAudit/Verified1–3/{external.json,startup.json,player.log}`, `verified-summary.json` — финальный Release.
- `Logs/LifecycleAudit/portable-verified-hashes.json` — все 198 SHA-256 совпали.
- `Logs/LifecycleAudit/Trading`, `InputLifecycle1–3`, `FirstDeal1–3` — полные отчёты и Player logs.
- `Logs/LifecycleAudit/ManualUI`, `AltF4` — реальные focus/pause/quit и exit code.
- `Logs/LifecycleAudit/StartupProfile` — raw CPU capture и расшифрованные samples.
- `Logs/LifecycleAudit/format.log`, `format-verify.log` — dotnet whitespace / verify exit 0; pre-commit hook повторил scoped formatting успешно. `git diff --check` и focused review выполнены.
- `Logs/LifecycleAudit/user-save-final-verification.json` — реальные saves неизменны.
- `Logs/LifecycleAudit/final-process-check.json` — 0 Player/crash-handler processes, 0 расхождений хешей проверенных source files. После независимых прогонов Editor снова запущен для исходного проекта.

Ранние результаты сохранены отдельно, не перезаписаны финальными GREEN. Локальные игнорируемые доказательства могут отсутствовать в другом клоне; один этот Markdown не заменяет повторный Player-прогон.
