# Аудит воспроизводимости, целостности и Windows build — 22.10.2026

## A. Executive verdict

**Да: в проверенном Windows-окружении Hustle & Scale восстанавливается из Git, компилируется и создаёт запускаемый Windows x64 Mono Player.** Проверены отдельный local clone и настоящий GitHub clone опубликованного `origin/dev`; после исправлений выполнен ещё один fresh clone с отсутствовавшими Library, Temp, UserSettings, IDE cache и новым пустым UPM cache. Пакеты/ассеты рабочего проекта в него не копировались.

Подтверждённых P0/P1 build/runtime блокеров в проверенных маршрутах нет. Исправлены остатки повреждённых rendering references и изменение generated source при первом импорте; добавлен небольшой Editor build entry point. Unity, зависимости, backend, pipeline, gameplay и утверждённый FIRST DEAL не менялись.

**Это функциональная воспроизводимость на этой машине, не обещание побайтовой идентичности или запуска на всех Windows/GPU.** Native UI Computer Use отказал в доступе к Player. Встроенные интеграционные input/UI/торговые сценарии прошли; физические клавиши и визуальная человеческая приёмка E6.6 этим не подтверждены.

**Исправления локальные.** Последняя проверка GitHub: remote/default HEAD и `origin/dev` = `65ef835086e582a74c6b7b66835f67fabcbced76`. Исправленный исходный commit = `a38f7e010d65c9be7fb5d85bc04ae2f09f5d135c`. Push/PR не выполнялись; remote сохраняет описанные ниже неблокирующие дефекты.

## B. Repository integrity

Начальное состояние: ветка `dev`, local HEAD `a00b4bfefbb04b3e2e1ef09434b7085a5a80f46e`, семь коммитов впереди опубликованного `65ef835`. Незакоммиченные пользовательские файлы: `Assets/_Recovery/0 (1).unity` и его `.meta`. Они не переносились в clones, не редактировались и не включались в коммиты. Существующий отслеживаемый `Assets/_Recovery/0.unity` — другой файл.

| Проверка | Наблюдение |
| --- | --- |
| Tracked sources | Начальный local SHA: 1057 файлов; remote SHA: 1038; исправленный source commit: 1059. В исправленном commit 830 путей под Assets, включая `.meta` |
| Assets/meta | Нет отсутствующих file/folder `.meta`, malformed или duplicate GUID. Пустая `Assets/Resources` не хранится Git как каталог; tracked folder meta позволяет Unity восстановить её. После импорта orphan meta нет |
| LFS | Git 2.54.0.windows.1, LFS 3.7.1; 133 LFS paths. Для всех сверены фактический SHA-256 и pointer OID: 0 несовпадений. `git lfs fsck` — OK. Remote clone скачал все 133 объекта в собственный `.git/lfs/objects` |
| Submodules | NOT APPLICABLE: `.gitmodules` и gitlinks отсутствуют |
| Generated output | Library, Temp, Logs, Builds, UserSettings/IDE caches не отслеживаются. Native binary plugins под Assets не найдены |
| `.gitignore` | Историческая проблема Packages уже устранена: root exceptions разрешают manifest и lock; оба tracked. ProjectVersion tracked и не игнорируется. `git check-ignore -v --no-index` подтвердил правила |
| `.gitattributes` | Существующие Unity YAML/eol и LFS macros сохранены. Binary LFS content hashes совпадают после checkout; line-ending corruption не выявлена |
| Git convenience | `core.autocrlf=true`, локальный `core.hooksPath=.githooks`; merge driver `unityyamlmerge` объявлен attributes, но executable driver в Git config не настроен. Это ограничение merge convenience, не зависимость build |
| `.editorconfig` | Сохранён; локальный commit hook отформатировал новый C# и проверил meta. Глобальная конфигурация Git не менялась |

GUID scan различает ссылки Assets и восстановленных packages. После исправлений: **221 внешний GUID разрешается в package assets, 0 неразрешимых GUID**. Это проверка GUID; полную корректность любого возможного fileID/игрового состояния она сама по себе не доказывает. Дополнительно выполнены настоящие Unity scene/asset проверки и Player scenarios.

## C. Toolchain и внешние требования

| Параметр | Проверенное значение |
| --- | --- |
| Unity / Hub | 6000.6.0f1, revision f7f8ed4d1e24; установлена в Hub layout, `metadata.hub.json`/`modules.json` прочитаны. Автоматическая установка/миграция не выполнялась |
| ОС / hardware | Windows 11 x64, Ryzen 7 3700X, 32683 MB RAM, RTX 4060 Ti; Player — Direct3D11 |
| Windows support | В Editor присутствуют win64 Mono development/nondevelopment variations; Windows IL2CPP module также выбран, но проверенный build его не использует. IL2CPP/MSVC/Windows SDK отдельно не верифицировались |
| Backend / API | Live API и BuildReports: Mono2x, stripping Disabled, API enum `NET_Standard_2_0`. Реальный Unity compiler response: `-langversion:9.0`, `NETSTANDARD2_1`, ссылки на bundled netstandard2.1 targeting pack |
| Rendering / input | URP 17.6.0; PC_RPAsset; Input System 1.20.1; uGUI 2.6.0. D3D11/D3D12 настроены, визуальные runtime результаты здесь относятся к D3D11 |
| License | Editor подтвердил Unity Personal entitlement. Ранние token/signature diagnostics не мешали последующим успешным import/build; это не основание объявить отсутствие лицензии |

Классификация окружения:

| Класс | Что требуется/для чего |
| --- | --- |
| Required | Точная Unity с Windows Mono support, её лицензия, Git/LFS, доступ к заявленным GitHub/Unity endpoints для cold restore, совместимые Windows/GPU/runtime |
| Optional tasks | Development и FIRST DEAL fixture builds; Blender для редактирования SourceArt. Для обычного build tracked FBX достаточно |
| Developer Convenience | Rider — основная IDE; Visual Studio integration, Editor MCP, VCS/Multiplayer Center tools, внешний MCP server и formatting hook. При текущем manifest их объявленные packages всё равно восстанавливает UPM; запущенные IDE/MCP server для batch build не нужны |
| Unexpected | Обязательных пользовательских абсолютных путей, manually installed runtime plugins, local/file dependencies или чтения исходного Library не обнаружено. Найденные stale serialized references и generated-header drift исправлены |

MCP Git package закреплён полным SHA; `LICENSE` на этом SHA прочитана через отдельный network fetch: MIT. Quaternius worker имеет tracked CC0 notice, Liberation Sans — OFL notice. Юридическая доступность старого `Male_Casual.fbx` для новых production assets уже отмечена в Art Direction как открытая; полной legal clearance всех legacy third-party assets этот build audit не устанавливает. Все необходимые для проведённых builds бинарные ассеты получены из Git/LFS без платных загрузок.

## D. Clean checkout: какие состояния проверены

| Состояние | Источник и исходная изоляция |
| --- | --- |
| Working Tree | Исходный проект; Editor уже открыт. Его Library не очищался, ветка не переключалась. Refresh/компиляция нового кода наблюдались отдельно |
| Clean Local baseline | Новый `git clone --no-hardlinks` из local `a00b4bf`; отсутствовали Library/Temp/UserSettings/.idea; отдельный CacheLocal. После первого сетевого отказа восстановление продолжено в этой копии; она не использовалась как единственное доказательство post-fix cold restore |
| Remote Checkout | Настоящий shallow clone `origin/dev` = `65ef835`; LFS получен с remote, отсутствовали Library/Temp/UserSettings/.idea; отдельный CacheRemote. После timeout одного registry download выполнен повтор |
| Clean fixed Local HEAD | Новый clone source commit `a38f7e0` в `Builds/ReproAudit20261022/Чистый проект With Spaces`; Library/Temp/UserSettings/.idea/CacheFinal отсутствовали. Cold package restore, import, compile, shader compilation и Player build прошли в первом запуске этой копии |

Для каждого состояния сохранены before-import JSON, команды, полные логи и exit records в `Logs/ReproAudit20261022`. В final import: 4525 actual imports, 1 local cache hit внутри этой новой import session, **cache server 0**, Accelerator disconnected. Никакие package sources или cached project assemblies из исходного проекта не подставлялись.

Первый sandbox local clone не смог создать Git subprocess; разрешённый запуск выявил старый Windows SID владельца `.git`. Точечные command-scoped `safe.directory` для проверенного source path позволили clone; глобальные security исключения/владельцы не менялись. Это особенность локального окружения, не требование обычного GitHub clone.

## E. Dependency restoration

`manifest.json`: 51 прямая запись. Lock: 65 packages — 52 builtin, 12 registry, 1 Git. Нет scoped registries, embedded/local/file packages, отсутствующих lock nodes или циклов dependency graph. Проверенные UPM восстановления не изменили manifest/lock.

Registry lock: AI Navigation 2.0.16; Collab Proxy 2.13.6; Rider 3.0.40; Visual Studio 2.0.28; Input System 1.20.1; Mono Cecil 1.11.6; Newtonsoft JSON 3.2.2; Pipeline 0.8.0-exp.1; Post Processing 3.5.4; Profiling Core 1.0.3; Searcher 4.9.5; Visual Scripting 1.9.12. Остальные builtin packages поставляются выбранным Editor. MCP 10.0.0: `d49ae2953580f3481beb1e084a1da2682f0b5610`.

UPM сообщил minimum-version overrides Newtonsoft 3.0.2 → 3.2.2 и Searcher 4.9.3 → 4.9.5. Lock уже содержит итоговые версии; это не новое неконтролируемое разрешение. Ненужные на первый взгляд Editor packages не удалялись ради прохождения проверки.

Реальные сетевые отказы сохранены: GitHub connection timeout/reset/early EOF при полном clone и Git package fetch; отдельный `download.packages.unity.com ETIMEDOUT` для VS integration. Уменьшенный shallow clone и command-scoped HTTP/1.1/retries восстановили работу. Закреплённый MCP SHA дополнительно успешно получен отдельным shallow fetch. CacheFinal восстановлен с нуля без изменения глобального UPM cache. Offline без сети/cache — **NOT TESTED**, проект этого не гарантирует.

## F. Unity import, C# и ссылки

Import и C# compilation наблюдались в изолированных проектах, затем выполнены настоящие builds. Runtime `UnityEditor` обращения в CitySaveController/DevelopmentSandbox находятся под `UNITY_EDITOR`; tests assembly имеет `includePlatforms: Editor`. Новая автоматизация находится под `Assets/Editor`. Unity-generated IDE projects и внешний .NET SDK не использовались как доказательство C# compatibility.

После завершения окончательных builds в fixed clone временно добавлен **только диагностический Editor probe**, затем удалены именно его `.cs`/`.meta`. Он не входил в ранее собранные Players и не исправлял исходники. Probe открывал все восемь tracked сцен без сохранения, проверял prefab GameObjects и ScriptableObject subassets:

- 8 сцен, включая CityGreybox, FirstDealTrade, остальные fixtures и tracked recovery scene: missing MonoBehaviours **0**;
- 18 prefabs: missing MonoBehaviours **0**;
- 142 `.asset`: ScriptableObjects with missing script **0**;
- DefaultVolumeProfile: 19 действующих компонентов, missing **0**.

`final-scene-probe.json` содержит отдельные пути/счётчики. После удаления probe `git status --porcelain` в fixed clone пуст; manifest/lock, settings и Assets равны commit.

При разработке нового entry point промежуточная компиляция обнаружила ошибку автора аудита `int` → `uint` в полях отчёта. Поля исправлены на реальные API types до implementation commit; последующие compilation/build/test проверки прошли. Это не исходный дефект проекта и не оставшийся compile blocker.

## G. Build configuration

Production scene list: **CityGreybox enabled**, **FirstDealTrade disabled**. FirstDealGraybox/Workshop, DevelopmentSandbox, ArtDirectionTest, SampleScene и recovery — отдельные fixtures; добавление их всех в production не требуется. Самостоятельного main-menu → FIRST DEAL перехода в текущем городе нет.

Активного сохранённого Build Profile нет; настройки берутся из tracked Player/Graphics/Quality/EditorBuildSettings. Target **StandaloneWindows64**, подтверждённый PE machine `0x8664`. Define — `UNITY_POST_PROCESSING_STACK_V2`; обычный build без Development/Profiler/autoconnect. Отдельные diagnostic builds явно используют Development. Fixture меняет только `BuildPlayerOptions.scenes`; общий scene list не меняется.

Editor quality index 1, Player index 0 имеют имя **PC**: Mobile исключён из Standalone, индексы перенумерованы. Startup reports подтверждают PC/PC_RPAsset, без переключения render pipeline. Native plugins, Addressables bundles и обязательные ручные world builders для import/build не требовались.

## H. Windows builds и repeatability

Исходные local `a00b4bf` и настоящий remote `65ef835` построены штатным `-buildWindows64Player`: логи содержат `Build Finished, Result: Success`, процессы вернули 0. Новый entry point сначала проверен в local clone с явной копией пяти scoped исправлений: `Succeeded`, errors 0, warnings 3. Это промежуточный dirty-source build, не выдаётся за post-fix clean proof.

Затем source commit `a38f7e0` проверен новым cold checkout:

| Окончательная сборка | BuildReport | Errors / warnings | Report bytes | Build time |
| --- | --- | --- | --- | --- |
| Production, cold | Succeeded | 0 / 3 | 112263212 | 95,72 с |
| Production, повтор того же output | Succeeded | 0 / 1 | 112263228 | 8,29 с |
| City Development | Succeeded | 0 / 3 | 192257075 | 39,74 с |
| FIRST DEAL Development fixture | Succeeded | 0 / 1 | 191668803 | 28,96 с |

Времена BuildReport не включают весь package restore/import и не являются benchmark производительности игры. При повторе C# уже кеширован, поэтому два CS0618 не переиздаются; уменьшение warnings не означает исправление deprecated API. Небольшое отличие размера повторной сборки допустимо: deterministic binary output не заявляется.

Обычный Player содержит exe, Data, UnityPlayer.dll, MonoBleedingEdge, D3D/runtime DLL и остальные Unity outputs. Полная **существующая** повторная Production-сборка скопирована в новую временную папку вне checkout с пробелами/кириллицей: **199 файлов** (включая JSON build report), **112263756 байт на диске**, **0 SHA-256 несовпадений**. Размер файлового дерева отличается от BuildReport summary по определению. Отдельной упаковки/ZIP/release archive не создавалось.

Оставшиеся предупреждения: PipelineRuntimeBuildProcessor не находит RuntimePipelineConfig у `com.unity.pipeline` и отключает runtime этого отдельного пакета; URP продолжает работать. Два runtime CS0618 — старые FindObjects API CityTradeWayfinding. В cold import есть legacy TrueTypeFontImporter warning LiberationSans meta; в Editor harness compilation — другие CS0618. Все полные логи сохранены. Эти сообщения не классифицированы как P0/P1 и не устранялись обновлением пакетов или отключением gameplay.

## I. Player smoke tests

| Запуск из окончательных outputs | Наблюдаемый результат |
| --- | --- |
| Production после cold build | READY, CityGreybox, PC/PC_RPAsset, errors 0, warnings 0, batchMode false, render submission 3,42 с, exit 0 |
| Production после повторного build | READY, те же scene/quality/pipeline, errors 0, warnings 0, render submission 3,52 с, exit 0 |
| Полная portable-копия вне checkout, cwd в output | READY, D3D11, те же scene/quality/pipeline, errors 0, warnings 0, render submission 3,35 с, exit 0 |
| City lifecycle Development | PASS **259 assertions**, exit 0: queued Input System E/Esc, первый seller interaction через PlayerRaycast, три New Game/menu/pause/save/load цикла, detached session, corrupt-save/backup recovery, реальный scene reload без дублей UI/систем |
| City prepare, процесс 1 | PASS **2201 assertions**, exit 0 |
| City continue, процесс 2 | PASS **1509 assertions**, exit 0 |
| City restore, процесс 3 | PASS **101 assertions**, exit 0 |
| FIRST DEAL Development fixture | PASS **105 assertions**, exit 0: A+B и B+A с ремонтом ($455), deadline/buyback ($275), finish и три scene restart |

Trading использует отдельный save и snapshots между процессами; пользовательские saves в этих harness/observer режимах не используются. Часть маршрута выполняется вызовами модели/кнопочных методов и перемещением fixture; это техническая интеграция, не самостоятельная ходьба человека. Assertion counts — не число NUnit tests/уникальных маршрутов/участников. Стартовая пауза (`canInteract=false` в startup snapshot) соответствует существующей политике; lifecycle test отдельно resume/взаимодействие проверяет.

Remote release Player запущен отдельно: Windows перечислил его окно `Hustle-Scale_dev`, Player.log создан. Старый remote commit **не содержит** lifecycle observer: первоначально переданный новый флаг был проигнорирован, автоматического report/exit не произошло. Только тот audit process остановлен явно. Повторный visible launch обнаружен window inventory; Computer Use вернул **`Computer Use was not approved to use hustlescale`**. После этого остановлен именно второй audit process; оба forced-cleanup exit −1 не выдаются за штатный выход/успешный observer test. Ошибок remote import/build этим не найдено; физический ввод/визуальный smoke/graceful quit remote остаются непроверенными.

## J. Подтверждённые дефекты и root cause

| ID / severity | Reproduction, expected → actual | Root cause / evidence | Fix / verification |
| --- | --- | --- | --- |
| RI-01 / P2 | `AssetDatabase.GetScriptableObjectsWithMissingScriptCount(DefaultVolumeProfile)`; ожидание 0 → **9** | Serialized subassets прежних sample/test/custom Volume types без доступных скриптов. Они не входят в 19 действующих компонентов. GUID scan + live AssetDatabase, `volume-cleanup.json` | Unity `RemoveScriptableObjectsWithMissingScript` и SaveAssetIfDirty удалили 9 subassets; значения/порядок 19 компонентов до/после совпали. Fresh Unity probe: 0 missing; build/render/smoke PASS |
| RI-02 / P3 | GUIDToAssetPath семи `PC_Renderer.probeVolumeResources` GUID; ожидание валидной ссылки/пустого поля → 7 dangling refs | Installed URP Deprecated.cs подтверждает перенос APV debug/runtime resources из этих устаревших полей; текущие targets отсутствуют | SerializedObject очистил только obsolete references; Unity сохранила актуальную representation/schema renderer. Asset GUID/renderer feature сохранены; final GUID scan 0, builds и реальный URP render PASS |
| RI-03 / P2 | Clean import → `git status --short`; ожидание чистого tracked state → IA_PlayerInput.cs modified | Wrapper с generated header 1.20.0 при pinned Input System 1.20.1; одинаковое изменение воспроизведено в local и remote clones | Перегенерация штатным importer. Diff wrapper — только версия header. Fresh cold import/tests/builds оставили tracked state чистым |

Сетевые сбои, старый owner SID, недоступность Computer Use и отсутствие тестов в старом SHA перечислены как ограничения окружения/проверки, не как выдуманные gameplay P0/P1. Неизвестная provenance отдельных legacy assets остаётся открытым release вопросом.

## K. Реализованные изменения

- `Assets/Settings/DefaultVolumeProfile.asset`: удалены девять неработающих subassets через Unity API; 19 действующих components сохранены.
- `Assets/Settings/PC_Renderer.asset`: убраны семь dangling obsolete APV ссылок, сохранено через текущую Unity. Нативная сериализация добавила текущие default fields/schema и убрала уже неиспользуемые поля; режим renderer, masks и SSAO parameters не изменялись вручную.
- `Assets/Scripts/Player/IA_PlayerInput.cs`: штатная перегенерация закреплённым importer, header 1.20.1.
- `Assets/Editor/WindowsPlayerBuild.cs` и `.meta`: минимальный Windows64 entry point на BuildPipeline с явным output, production/fixture/Development flags, BuildReport JSON, ошибками/exit code. Не создаёт собственную build framework/CI.
- Build guide, этот отчёт и короткие ссылки README/DEVELOPMENT.

Отрицательный workflow test: относительный `-buildOutput relative.exe` отклонён с **exit 1**, до создания Player. Исходный рабочий Editor после исправления C# компилировался; основная окончательная проверка выполнена в source-only clones. Баланс, игровые переходы, Unity/packages/settings платформы и shared scene list не менялись.

## L. Verification matrix

PASS ниже относится только к указанному уровню; чтение файлов не выдаётся за выполненный Player/test run.

| Проверка | Local Project / Working Tree | Clean Local HEAD (a00 baseline → a38 final) | Remote Checkout 65ef835 |
| --- | --- | --- | --- |
| Наличие tracked sources/meta | PASS, file audit | PASS, file audit | PASS, file audit |
| LFS content/hash | PASS | PASS | PASS, network clone |
| Packages restore с новым cache | NOT TESTED: существующий рабочий cache сохранён | PASS, новый CacheFinal | PASS после network retry, отдельный CacheRemote |
| Unity import | PASS refresh; не cold import | PASS cold | PASS после network retry |
| C# compilation | PASS после правки entry point | PASS | PASS |
| GUID integrity после restore | PASS исправленный source | PASS, unresolved 0 | FAIL: 11 stale GUID, неблокирующие RI-01/02 |
| Открытие всех сцен / missing-script probe | NOT TESTED | PASS: 8 scenes / 18 prefabs / 142 assets | NOT TESTED: отдельный probe не запускался |
| EditMode tests | NOT TESTED в рабочей сессии | PASS 6/6 baseline и 6/6 final | NOT APPLICABLE: suite отсутствует, XML 0 tests |
| Отдельная PlayMode test assembly | NOT APPLICABLE | NOT APPLICABLE; шесть Editor UnityTests реально входят в Play Mode | NOT APPLICABLE |
| Windows x64 build | NOT TESTED: исходный рабочий проект не собирался | PASS baseline, final cold и repeat | PASS production |
| Player launch | NOT TESTED | PASS: 3 production/portable READY, exit 0 | PASS process/window registration; дальнейший native UI smoke BLOCKED |
| Автоматический city/input/save smoke | NOT TESTED | PASS lifecycle + три процесса торговли | NOT TESTED |
| FIRST DEAL smoke | NOT TESTED | PASS отдельная fixture, 105 assertions | NOT TESTED |
| Штатный Player quit | NOT TESTED | PASS observer/harness exit 0 | NOT TESTED: audit processes остановлены явно |
| Повторный build/launch | NOT TESTED | PASS | NOT TESTED |
| Путь с пробелами/кириллицей | NOT TESTED | PASS project build и portable Player launch | NOT TESTED: обычный путь |
| Physical OS keyboard / visual acceptance | BLOCKED Computer Use для Player | BLOCKED Computer Use; queued Input System checks PASS | BLOCKED Computer Use |

## M. Оставшиеся ограничения

1. Второй независимый компьютер, другие GPU/OS, IL2CPP и offline restore без cache не проверены. Активированная Unity и доступность внешних package/LFS endpoints остаются реальными требованиями.
2. Source fixes/new workflow ещё не опубликованы. Remote build работоспособен в наблюдённых пределах, но remote source integrity не равна исправленному local SHA.
3. Три production startup runs и focused scenarios не покрывают всё игровое пространство, длительный soak, аудио или все save histories. Editor во время standalone тестов мог оставаться открыт; независимость от его процесса на этом этапе отдельно не проверялась.
4. Computer Use app access denied: вид окна/физический ввод и человеческая E6.6 acceptance не закрыты. Отказ не обходился сторонней OS UI automation.
5. Остались перечисленные pipeline/deprecated/importer warnings и вопрос legacy asset provenance. Build/runtime P0/P1 на проверенных сценариях не воспроизведены.

## N. Документация и evidence

Актуальные инструкции: **[BUILD_AND_REPRODUCIBILITY.md](BUILD_AND_REPRODUCIBILITY.md)**. Подробные правила saves/input/harness — [Player lifecycle audit](PLAYER_STARTUP_LOADING_LIFECYCLE_AUDIT.md), текущее игровое принятие — [PLAYTEST](PLAYTEST.md).

Полные локальные доказательства в игнорируемом **`Logs/ReproAudit20261022/`**:

- baseline/network: `local-clone*.log`, `remote-clone*.log`, `remote-shallow.log`, `dependency-fetch.log`, `local-import.log`, `remote-import.log`, baseline build logs и exits;
- source integrity: `integrity.py`, `final-integrity.json`, `remote-integrity-after.json`, `volume-cleanup.json`, `compiler-profile.json`, `player-architecture.json`;
- clean proof: `*-before-import.json`, `final-cold-build-command.json`, `final-cold-build.log`, `final-cold-build-exit.json`, сохранённый `final-cold-build-report.json`;
- tests/builds: `local-editmode.xml`, `final-editmode.xml`, `remote-editmode.xml`, полные одноимённые logs/exits; `final-builds-summary.json`, `final-repeat-build*`, `final-development-build*`, `final-firstdeal-build*`, `workflow-invalid-output*`;
- assets: `ReproIntegrityProbe.cs`, `final-scene-probe-command.json`, `final-scene-probe.log`, `final-scene-probe.json`, exit record; probe был добавлен только после builds и удалён из checkout;
- Players: `FinalStartup1`, `FinalStartup2`, `PortableStartup`, `FinalCityLifecycle`, `FinalTrading`, `FinalFirstDeal`, `portable-copy.json`, remote launch logs/intentional forced exits и `computer-use-blocker.txt`.

В Player папках находятся command/process/exit JSON и полный Player.log, рядом — startup/scenario reports и изолированные saves. Команды audit wrappers сохранены рядом; guide использует те же проверенные Unity arguments. Сборки остаются в `Builds/ReproAudit20261022/`, portable copy location записан только в локальном JSON; личные абсолютные пути/credentials в этот отчёт не добавлялись. Эти логи/outputs не versioned; перенос отчёта без них сохраняет выводы, но не полные raw evidence.

## O. Git и завершение

Implementation commit: **`a38f7e0 fix(build): stabilize clean imports and expose Windows builds`**. Hook выполнен, scoped staged diff проверен. Этот отчёт/guide и навигационные ссылки фиксируются отдельным documentation commit после проверки ссылок и `git diff --check`.

Documentation commit не меняет Assets/Packages/ProjectSettings; проверенное build-содержимое остаётся равным `a38f7e0`. Для четырёх Markdown-файлов проверены 70 локальных ссылок (0 отсутствующих targets); семь PowerShell code blocks guide разобраны PowerShell parser (0 ошибок), передача массива аргументов с пробелами проверена отдельно.

Рабочая ветка `dev` сохранена; push, PR, reset, switch, переписывание истории и удаление исходного Library не выполнялись. Единственные оставшиеся чужие untracked paths — два начальных recovery файла. Fixed verification clone после всех проверок чист. README не должен трактовать этот результат как уже опубликованные fixes или человеческую приёмку игры.
