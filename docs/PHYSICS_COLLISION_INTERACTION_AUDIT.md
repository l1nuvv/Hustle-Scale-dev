# Physics, collision and interaction audit — 22.10.2026

## A. Executive verdict

Аудит начат на `dev`, HEAD `ee51e8f371e15833957dc8e72d9e33c4623ab66e`. Unity **6000.6.0f1**, Windows Editor / Windows64 Mono Player. Проверяются отдельно основной город `Assets/Scenes/CityGreybox.unity` и утверждённый fixture `Assets/Scenes/FirstDealTrade.unity`; `FirstDealGraybox` и `SampleScene` используются существующим T02. E6.6 остаётся человеческой приёмкой.

Подтверждены и исправлены два дефекта: Q мог поставить товар внутрь игрока; E при повороте мышью в том же кадре использовал прежнюю камеру. P0 в выполненных проверках не обнаружен. Это ограниченный технический аудит существующих сцен и маршрутов, а не доказательство достижимости каждой точки города или человеческого удобства. Ни новые игровые системы, ни изменение баланса не требовались.

## B. Physics configuration

Источник: текущие `ProjectSettings/DynamicsManager.asset`, `TimeManager.asset`, `TagManager.asset`, сцены и наблюдаемый Editor. URP 17.6.0, Input System 1.20.1, Test Framework 1.8.0; зависимости и rendering pipeline не менялись.

| Параметр | Значение / следствие |
| --- | --- |
| Gravity | `(0, -9.81, 0)` |
| Fixed / maximum timestep | `0.02 / 0.33333334` с |
| Default contact offset | `0.01` м |
| Solver iterations / velocity iterations | `6 / 1` |
| Queries hit backfaces / triggers | false / true; конкретные запросы задают свой trigger policy |
| Auto sync transforms | false; harness явно синхронизирует изменённые тестовые позы |
| Simulation | Runtime `Physics.simulationMode = FixedUpdate`; глобальная конфигурация не менялась |
| Layer Collision Matrix | Все 32×32 пары включены в serialized matrix |
| Определённые слои | Default 0, TransparentFX 1, Ignore Raycast 2, Interactable 3, Water 4, UI 5, PostProcessing 8 |

Игрок города в runtime получает слой **Ignore Raycast**, сохраняя столкновения. Мир, товар и игровые точки преимущественно на Default; отсутствие отдельных слоёв Player/Items/NPC само по себе не дефект. Player↔World/Items/NPC/Interactive и Items↔World/Items не исключены matrix. Raycast-mask проверяется отдельно от matrix: `Physics.DefaultRaycastLayers` исключает игрока, но физические контакты сохраняет. Это различие было причиной PC-01. Новые слои не создавались.

## C. Player movement

`PlayerController` использует `CharacterController.Move` в Update, ходьба 4 м/с, Shift ×1,5, нормализованный двумерный ввод. Mouse delta не умножается на deltaTime. Камера обновляется в LateUpdate; eye offset `(0, 1.65, 0)`, pitch ±85°. Гравитация интегрируется через deltaTime, grounding задаёт отрицательную прижимающую скорость, предел падения −40 м/с. Rigidbody контроллера нет.

Наблюдаемая городская капсула: height 1,8; radius 0,3; center y=0,9; skin 0,03; step 0,3; slope limit 45°; min move 0,001 м. Включённость и поза временно изменяются только при штатном восстановлении позиции/переходе в машину либо внутри изолированного теста.

S3 baseline прошёл 39 750 assertions, включая коллизии и перенос по торговым маршрутам. Новые проверки используют настоящую капсулу: стена останавливает, диагональ скользит вдоль стены, проход 0,8 м доступен, 0,4 м блокируется; угол останавливает обе оси и допускает выход задним ходом. Это временная тестовая геометрия, не новые authored assets. На четырёх реальных внешних дорогах капсула достигает стены около 496 м, сохраняет опору и не выходит за границу. T02 подтвердил grounding, высоту камеры, WASD/диагональ/спринт, стену и ступени 0,2 м. Прыжок, приседание и новый контроллер не добавлялись. Подвижные платформы и отдельная игровая система наклонных маршрутов не реализованы; исчерпывающий тест произвольных склонов не выполнялся.

## D. Scene collisions

До городского Start обнаружены 370 Collider. В активной runtime-иерархии после композиции — 467 Collider-компонентов, включая отключённые, 0 trigger; распределение по слоям 466 Default + 1 Ignore Raycast. Это не число активных native shapes: выключенные коллайдеры и compound/wheel shapes учитываются иначе в Profiler.

Найден один явный Rigidbody — `Workshop electric utility`: масса 650, gravity on, non-kinematic, Interpolate, ContinuousDynamic, constraints None. WheelCollider работают через существующий FixedUpdate; физическое ускорение машины не заменялось перемещением Transform. Основные поверхности, мебель и интерактивные proxy используют существующие простые коллайдеры. Массовая замена на MeshCollider не выполнялась.

**QA-01 — устаревшие маршруты, P2 проверки.** `CityGreyboxVerification.CollisionRoutes` останавливается на прямой `(0,96) → (0,-288)`: она пересекает `Building | Market south tall shop`, bounds center `(-3,4,-31)`, extents `(7,4,7)`. Local routes 0/1/2/3 также не проходят старую проверку габаритов ящика; local 4/5/6 проходят (422,7 / 465,1 / 303,5 м). Это FAIL данных маршрута, не доказательство прохождения сквозь дом или блокировки обязательной торговли. S3 проверяет актуальные обходные торговые маршруты. Геометрия ради старых прямых не менялась. Доказательство: `Temp/PhysicsAudit/city-collision-routes.txt`; очередь — пересмотреть старые waypoints по текущему району и отдельно повторить, если эти маршруты понадобятся.

## E. Interaction system

Один ближайший `Physics.Raycast` из FPS-камеры, дальность 2,5 м. Ближайшее препятствие блокирует цель за ним. Для PlayerRaycast явно задан `QueryTriggerInteraction.Collide`, что сохраняет legacy pickup с trigger; в основном городе trigger не обнаружены. Размещение, проверка поверхности и городской `Reachable` используют Ignore. Нет новой RaycastAll/NonAlloc-системы; существующий vehicle approach использует свой RaycastAll и исключает собственную машину.

Контракт иерархии: обработчик находится на объекте Collider либо на его предке. Поиск произвольного обработчика на соседнем child не предусмотрен и в проверенных игровых prefab не потребовался. Новый тест подтверждает Collider на child и обработчик на parent. Цель не кешируется между E и исполнением: после обновления камеры выполняется свежий hit. Отключение PlayerRaycast очищает ожидающее нажатие и prompt.

Проверены расстояния до **поверхности** Collider 2,49/2,51 м, стена, повтор/удержание E, поворот к цели и от неё, удалённая цель, уничтоженная цель и блокировка UI. `Reachable` дополнительно проверяет точку, активность, Collider, расстояние ClosestPoint и linecast к центру. Частичная видимость большого Collider не исследована исчерпывающе.

## F. Accessibility matrix

В городе инвентаризированы все 15 runtime-точек, включая дублирующие вывески и три фонаря. Для 12 точек с включённым Collider выполнена круговая выборка — по 16 поз на расстоянии 2 м с наведением в центр Collider; это **проверка обнаружения**, не навигация: часть поз может находиться с недоступной стороны. Во всех случаях фактический hit целевого Collider согласовался с `Reachable` (0 ложных отказов в этой выборке). Три фонаря в исходном seller-состоянии имеют отключённый Collider и проверяются после переноса отдельно.

| Объект | Пространство / обнаружение | Логика и feedback / границы evidence |
| --- | --- | --- |
| Городской старт / выход из мастерской | S3 торговые маршруты проходят | Пауза старта; возврат управления проверяется S3/UTF |
| Продавец `(88,1.3,-37)` | 13/16 лучей; вывеска также 13/16 | Покупка и повторный отказ — S3; часть ракурсов закрыта геометрией |
| Батареи `(65,1.1,26)` | 16/16; путь туда/обратно S3 | Расход денег/батареи и отказы — существующие игровые harness |
| Покупатели A/B `(-29/-7,1.3,10.5)` | Основные proxy и вывески: 16/16 | Передача только предъявленного подходящего товара, однократная выплата |
| Скупка `(-65,1.1,-47)` | 16/16; S3 маршрут с товаром | Диагноз/владение/повторная продажа ограничены моделью |
| Ящик у продавца `(83,0.4,-35)` | 16/16; Collider только когда стоит | Q исправлен; перенос/свободная постановка/груз — профильные проверки |
| Верстак `(-72,0.61,34)` | 10/16; доступны рабочие стороны | E положить/забрать; T/R — отдельные callbacks, не окно мастерской |
| Тестер `(-71,1.28,34)` | 11/16; часть ракурсов закрыта столом | Тот же workshop contract; не отдельная экономика |
| Три городских фонаря | Seller/Crate/Hands не имеют активного pickup Collider по правилам; Bench/World имеют | Индивидуальный ID/диагноз; размещение и перенос проверяются отдельно |
| Багажник / Tailgate | 13/16; некоторые стороны закрыты машиной | Только остановленная машина, допустимое состояние рук/ящика |
| FIRST DEAL: доска, продавец, селекторы 0–2, ящик, док | Реальные сцена/адаптеры и E; T04 | Отдельная фиксированная партия, не RNG города |
| FIRST DEAL: стол, TEST, REPLACE BATTERY | Реальные proxy; тест/ремонт/повторный тест | Предмет на столе и запасная батарея обязательны |
| FIRST DEAL: A, B, Repeat B, buyback, finish/restart | Отдельный fixture и существующие маршруты/модель | Условия и денежные результаты не заменялись городскими правилами |

Числа выборки: `Temp/PhysicsAudit/point-sampling.txt`; городская inventory: `runtime-inventory.txt`. Физическая доступность NPC обеспечивается существующими торговыми proxy/вывесками, не попыткой сделать интерактивной каждую декоративную часть персонажа. Самостоятельные NPC, расписания, свободное физическое бросание товара и подвижные платформы — **NOT IMPLEMENTED**, не новые задачи этого аудита.

## G. Item physics

Торговые фонари — конечные модельные экземпляры с управляемым представлением, без Rigidbody/массы/свободного падения. Городской pickup BoxCollider `0.30×0.48×0.26` м, ящик `1.25×0.35×0.65` м. В Hands/Pocket/Crate коллайдеры фонаря выключены, Pocket скрыт; в Bench/World включены. Ящик в руках/машине не блокирует луч и капсулу. В FIRST DEAL сохраняются prefab-визуалы, world pickup и выключение collider у проданного экземпляра.

Q проверяет горизонтальную поверхность, видимость и свободный объём до модельной мутации. После PC-01 проверка объёма включает капсулу игрока. Неудача сохраняет ID/владение/позу; корректная постановка включает Collider и не пересекается с игроком по `ComputePenetration`. Штатные saves/harness проверяют восстановление world-представления; реальное падение или бросок фонаря неприменимы.

## H. Input / UI

Gameplay владеет существующей runtime InputActionMap. E использует edge, удержание не повторяет действие. После PC-02 запрос E исполняется в `PlayerRaycast.LateUpdate` с execution order 50, после камеры. Свежая проверка `CanInteract`/инвентаря выполняется при исполнении. Карта/пауза и курсор используют прежних владельцев; второй input-map не создавался.

T02 прошёл 23 проверки, включая прототипный Tab, trigger pickup, Esc/click, мышь и UI lock. Новый общий тест подтверждает E/W на паузе и E после выхода. OS Alt+Tab/minimize в этом аудите отдельно не повторялись; прошлые результаты lifecycle-аудита не выдаются за свежие.

## I. Timing and frame rate

Общий тест проверил настоящий PlayerController.Update с заданным `Time.captureDeltaTime`: 15/60/144 Гц. Ходьба и диагональ: 3,2000 м/0,8000 с; 0,8000 м/0,2000 с; 0,3333 м/0,0833 с. Спринт: 4,8000 / 1,2000 / 0,5000 м за те же интервалы. Проверено отсутствие перемещения при timeScale=0. Это контролируемое моделирование интервала кадра, а не измеренный render FPS.

Реальный FPS измеряется отдельно с `captureDeltaTime=0`. В финальном UTF Editor: 149,2 Гц, максимальный кадр 8,00 мс на 32 отсчётах. Три отдельных Windows Player-процесса при target 15/60/144 дали измеренные 15,0/60,0/144,0 FPS и max frame 66,68/16,67/6,95 мс. В каждом прошёл один и тот же набор физических/интеракционных проверок, 497 harness assertions. Значения зависят от машины/режима и не являются бюджетом выпуска. Fixed timestep и maximum timestep не менялись. T/R/Q/F города по-прежнему вызываются в фазе исходного input callback; быстрый поворот одновременно с этими клавишами не покрыт исправлением E и остаётся отдельным риском для проверки. Длительные тяжёлые кадры/произвольные timeScale кроме нуля не измерялись.

## J. Performance

Измерение штатными `ProfilerRecorder` категории Physics, 32 обычных кадра города, без deep profiling. Все recorder valid. Среднее включает нулевые отсчёты и все физические шаги кадра: это не стоимость одного шага или одного луча. На 15 FPS кадр содержит больше fixed steps.

| Среда | Physics.Simulate, мс/кадр | Physics.SyncTransforms, мс/кадр | Physics Queries / кадр |
| --- | ---: | ---: | ---: |
| UTF Editor, 149,2 FPS | 0,0533 | 0,0164 | 8,1 |
| Windows Player, 15 FPS | 0,3297 | 0,0234 | 2,1 |
| Windows Player, 60 FPS | 0,1232 | 0,0218 | 2,4 |
| Windows Player, 144 FPS | 0,0430 | 0,0158 | 4,2 |

Raw значения/условия — `Temp/PhysicsAudit/NUnit-814554666e584db98f0787d33c1bf0bc/checks.log` и `Player-{15,60,144}/physics-report.json`. Editor и Player не сравниваются как before/after: у них разные служебные нагрузки.

Подтверждённой проблемы стоимости физики не найдено в этом коротком замере. Не делается before/after-утверждение об ускорении; CPU/GPU/full-world budgets и массовый ассортимент не оценивались. Избыточный framework, pooling, NonAlloc API или глобальный SyncTransforms в Update не добавлялись.

## K. Reproduced bugs

### PC-01 — P1: постановка внутрь игрока

- Scene/GameObject: CityGreybox, City Player / Ящик партии / отдельный фонарь.
- Source: `Assets/Scripts/Interactables/CityTradeView.cs`, `DropHeld`.
- Reproduction: купить партию, взять ящик, встать `(-65,0.03,25)`, pitch 85°, нажать Q.
- Expected: пересечение с игроком запрещает постановку, предмет остаётся в руках, состояние не меняется.
- Actual: Q возвращает true; ящик ставится около `(-65,0.19,25.15)`, `ComputePenetration` с капсулой = true, глубина **0,155 м**. Это подтверждённое пересечение, а не утверждение о безвыходном soft-lock.
- Root cause: overlap-mask `Physics.DefaultRaycastLayers` исключал игрока на Ignore Raycast. Маска обнаружения ошибочно использовалась для проверки занятого объёма.
- Fix: только occupancy CheckBox использует все слои, triggers по-прежнему Ignore. Лучи по-прежнему исключают игрока.
- Regression: Q у ног для ящика и фонаря, атомарный отказ, безопасная постановка с yaw 45°, отсутствие пересечения после успеха.
- Evidence: `q-at-feet-baseline.txt`, `q-regression-red.log`, `q-regression-green.log`; commit `544e6d2`.

### PC-02 — P1: E использует камеру прошлого кадра

- Scene/GameObject: общий PlayerRaycast, включая городской продавец и child-collider fixture.
- Source: `Assets/Scripts/Player/PlayerRaycast.cs`, PlayerInput.Update → PlayerController.Update/LateUpdate.
- Reproduction: при yaw 90° от цели отправить mouse delta −750 (sensitivity 0,12) и E в одном кадре.
- Expected: действие соответствует новой видимой камере; поворот от цели не активирует прежнюю цель.
- Actual: yaw становится 0°, prompt продавца присутствует, магазин не открывается. Исходный общий тест падает на same-frame E.
- Root cause: OnInteract исполнял raycast до Update движения/обзора и LateUpdate камеры.
- Fix: callback запоминает один запрос, LateUpdate после камеры делает свежий raycast и повторно проверяет разрешение. OnDisable очищает запрос.
- Regression: поворот к/от цели, child Collider, повтор/удержание, стена, distance boundary, UI, уничтожение цели; те же проверки исполняются в Player.
- Evidence: `turn-e-baseline.txt`, `turn-e-regression-red.log`, `interaction-regression-green.log`; commit `5ca9dab`.

### QA-02 — P2 проверки: T04 требует старую стартовую сцену

T04 остановился на assertion «build list starts the playable T04 scenario». Текущий утверждённый build starts CityGreybox; FirstDealTrade — отдельный fixture. Исправлено только ожидание: одна enabled CityGreybox, текущий тест явно загрузил FirstDealTrade. Build settings не менялись. Исходный FAIL сохранён в `t04-stale-precondition-red.log`.

## L. Implemented changes

Игровые правки ограничены `CityTradeView.cs` и `PlayerRaycast.cs`. `PhysicsInteractionVerification.cs` содержит общий сценарий для существующего изолированного S3 Editor runner и opt-in Development Player (`CityPlayerVerification`, phase physics). В `Assets/Tests/Editor` добавлена одна test assembly и один UnityTest-адаптер, вызывающий тот же сценарий через одну публичную точку reflection: asmdef не может прямо ссылаться на predefined Assembly-CSharp. Runtime assembly boundaries не менялись.

UTF-адаптер сохраняет исходную сцену/input/background settings, отдельный save override, удаляет свои устройства и выходит из Play Mode в teardown. Замыкания создаются после EnterPlayMode, поскольку восстановление iterator после domain reload не восстанавливает C# display class. Два первоначальных FAIL адаптера сохранены; они не являются игровыми дефектами. GUID существующих assets, сцены, prefab, settings, цены, сроки и результаты диагностики не изменялись.

## M. Regression evidence

Доказательства локальные, `Temp/PhysicsAudit/` игнорируется Git; на другом checkout могут отсутствовать. Assertions harness не являются количеством NUnit tests или независимых игроков.

| Проверка | Свежий результат / доказательство |
| --- | --- |
| S3 baseline | PASS 39 750 assertions; `s3-baseline.log` |
| Новый Q regression | RED на исходном коде → GREEN 20 assertions; оба журнала сохранены |
| Новый interaction/movement harness | RED same-frame E → GREEN 230 assertions; `interaction-regression-green.log` |
| T02 Graybox / SampleScene | PASS 23 проверки; `t02-final.log` |
| Unity Test Framework, финальный код проверок | **1 passed, 0 failed**, EditMode UnityTest с реальным EnterPlayMode/ExitPlayMode; `nunit-final.xml`, `NUnit-814554666e584db98f0787d33c1bf0bc/checks.log` |
| FIRST DEAL T04, A → B и B → A | Оба PASS, по 107 проверок; реальные E/коллизии/маршрут, модельная подготовка диагноза/ремонта; `t04-a-then-b-final.log`, `t04-b-then-a-final.log`. Ходьба 14,58/17,35 игровых секунд — скриптовые маршруты, не человеческие времена |
| FIRST DEAL T05 | PASS 88 проверок: скупка, Repeat B, summary/finish/restart, диагностика/ремонт; `t05-final.log` |
| FIRST DEAL T06 domain | PASS 222 310 assertions, 64 seeds × 256 действий; `t06-final.log` |
| E6 domain | PASS 8 651 assertions; `e6-domain-final.log` |
| E5.7 Play Mode | PASS 864 assertions: постановка, E/T/R, repeated loads, world/vehicle cargo, recovery; `e57-final.log` |
| E6 Play Mode | PASS 282 assertions, обе партии и повторные загрузки; `e6-final.log` |
| Windows Player physics, три процесса | Все PASS 497 assertions, exit 0, actual 15/60/144 FPS; `Player-{15,60,144}/physics-report.json` и `player.log` |
| Windows Player trading, три процесса | prepare/continue/restore PASS 2201/1509/101 assertions, exit 0: перенос, ремонт/повторный тест, B → A, save/restart, вторая партия; `Player-Trading-Final/*-report.json` |
| Дополнительный Editor negative | PASS Tab+E в одном кадре блокирует world action; реальный Collider ящика перед child-целью блокирует E к ней; `extra-negatives.txt`. Отдельная runtime-инъекция в изолированном save, без изменения сцены |

При extra-проверке первоначальная ручная перестановка одного Transform была отменена штатным `Refresh`, восстанавливающим позу из World. Это невалидная предпосылка теста: к кадру E ящик уже стоял у продавца. После задания стабильной тестовой позы через существующий World и подтверждения ближайшего hit настоящий ящик блокирует цель. Оба результата и координаты оставлены в `extra-negatives.txt`; исходный FAIL не объявлен игровым дефектом.

UTF первый раз запущен `run_tests(mode=EditMode, assembly_names=[HustleScale.Physics.Tests])`. MCP после domain reload сохранил устаревший running job `a31586395cb74bc3bda6dfa1719528d7`; нативный UTF завершился. Повтор — штатный `TestRunnerApi.Execute` с тем же фильтром; итог проверен в нативном XML `Application.persistentDataPath/TestResults.xml`, скопированном в `nunit-final.xml`. Устаревший статус MCP не используется как доказательство PASS.

Windows64 Development build через `BuildPipeline.BuildPlayer`, одна сцена CityGreybox, Development + DetailedBuildReport: **Succeeded, 0 errors / 4 warnings**, 26,53 с, 192 221 979 байт; GUID `0bb25625cac14ae697207bf0d0ad25d2`. Тестируемый код: `5ca9dab` + финальные audit-tests этого отчёта. SHA-256 `HustleScale_Data/Managed/Assembly-CSharp.dll`: `019072DC919D650D6B7EDE1C8B45C500579ED95245D774A5A11D6086E13A2D55`. Предупреждения: прежние RuntimePipelineConfig и два obsolete FindObjects; дополнительно «uncompiled code changes» для Editor post-processing/import code после UTF-generated resources. Runtime содержит финальные проверки угла/границ и оба исправления, что подтверждено всеми Player physics runs. Изменений importer/postprocessor в задаче нет; предупреждение не скрыто. Полные сообщения — `build-messages.txt`, summary — `build-summary.txt`, полный Editor-журнал — `editor-full.log`.

Автоматические Player-процессы запускаются последовательно, 1280×720 windowed, синтетические Input System устройства, runInBackground в harness; это не OS/human input playtest. Верификационная папка `Builds/PhysicsAudit`, без ZIP/release packaging. Пример нового fresh-run (output должен быть новым):

```powershell
$auditOut = Join-Path (Get-Location) 'Temp/PhysicsAudit/new-player-run'
New-Item -ItemType Directory -Path $auditOut -Force | Out-Null
& ./Builds/PhysicsAudit/HustleScale.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 "-city-verify=$auditOut" -city-verify-phase=physics -physics-fps=15 -logFile "$auditOut/player.log"
```

Для trading phase prepare/continue/restore используется одна отдельная папка; после каждого успешного выхода копировать `city-save.json` в `<phase>-save.json` перед следующим процессом. Первая попытка continue ошибочно запущена без `prepare-save.json`: ожидаемый отказ чтения precondition, exit 4, `Player-Trading/continue-report.json`. После исправления процедуры все три фазы повторены в свежей `Player-Trading-Final` и прошли. Это ошибка процедуры запуска, не исправление экономики/сохранений.

### Negative matrix

| Сценарий запроса | Evidence / граница проверки |
| --- | --- |
| 1. Вплотную к стене | T02 и общий physics test, PASS |
| 2. Диагональ вдоль препятствия | Реальная капсула, PASS |
| 3. Узкий угол и выход из него | Общий physics test, PASS |
| 4. Между двумя предметами | Зазоры 0,8/0,4 м в тестовой геометрии, PASS |
| 5. Граница дистанции | Collider front 2,49/2,51 м, PASS |
| 6. Через стену | Prompt/action блокируются, PASS |
| 7. Через другой предмет | Отдельный Editor test с настоящим Collider ящика, PASS |
| 8. Дочерний Collider | Parent handler, PASS |
| 9. Поворот + E | RED → GREEN; Player при трёх FPS, PASS |
| 10. E дважды | Два отдельных edge, PASS; повторная выплата отдельно T04/E6 |
| 11. Удержание E | Один вызов, PASS |
| 12. Уничтожаемая цель | Свежий raycast / нет stale prompt, PASS |
| 13. Открыть инвентарь/карту во время E | Tab+E в одном кадре, world action заблокирован, PASS |
| 14. Закрыть UI и повторить | UTF / S3 / T02, PASS |
| 15. Restart сцены | T05 fresh $300/introduction; E5.7 реальная загрузка, PASS |
| 16. Повторно подобрать товар | E5.7 restored world lamp; model ownership / placement tests, PASS |
| 17. Повторная покупка | S3 / E6 / Player trading, атомарный отказ, PASS |
| 18. Недопустимое состояние станции | Domain/harness refusals T06/E6/T05, PASS; не доказательство доступности произвольной новой станции |
| 19. NPC с разных сторон | 16 поз обнаружения на enabled proxy, результаты в F; не полный pathfinding proof |
| 20. Границы локации | Четыре реальные внешние дороги, UTF + Player, PASS |
| 21. Предмет после изменения состояния | World/Hands/Bench/Pocket/sold, E5.7/T05, PASS |
| 22. Низкий FPS | Фактический Player 15 FPS и controlled deltaTime, PASS |

Неприменимо к текущему контракту: свободные Rigidbody-броски фонарей, потеря падающего товара, движущиеся платформы, динамические NPC/trigger zones города. Никакие отсутствующие механики ради этих сценариев не добавлялись.

## N. Remaining risks and next gate

1. Человеческая E6.6 не закрыта автоматикой. Требуется наблюдаемое удобство переноски/мастерской/второй партии.
2. QA-01: старые global/local collision waypoints требуют отдельного согласования с нынешними зданиями. Их FAIL сохранены, не скрыты успешным S3.
3. Не доказаны все углы/границы каждого декоративного объекта, произвольные slopes, платформы и длительные high-load frame spikes. Подтверждённого нового P0 в проверенной области нет.
4. T/R/Q/F одновременно с быстрым поворотом остаются отдельной проверкой фазы ввода. PC-02 исправляет E; нельзя переносить его PASS на другие callbacks.
5. Не выполнялись независимые человеческие маршруты, новый OS-focus тест и выпуск на другом компьютере. Старый lifecycle audit остаётся историческим evidence.

## O. Git

Локальные игровые коммиты: `544e6d2` и `5ca9dab`. Коммит аудита, содержащий этот отчёт, сохраняет общий Player profiler/дополнительные collision checks, UTF-адаптер с `.meta`, актуализированный T04 и ссылки workflow. Проверки: scoped `dotnet format whitespace`, `git diff --check`, focused diff review, существующие GUID/scene/prefab paths не менялись. Полные результаты выше.

BuildPipeline пересериализовал `PlayerSettings`: runtime background override и ранее встречавшийся в открытом Editor суффикс `i` у `m_StackTraceTypes`. Обе строки возвращены к точным значениям чистого файла на старте задачи; настройки не входят в commit, runtime runInBackground возвращён в false. UTF-generated performance JSON после build отсутствуют. Исходно неотслеживаемые `Assets/_Recovery/0 (1).unity` и `.meta` сохранены отдельно. Нет смены ветки, reset, push, миграции Unity/packages, release package или ZIP. Открытая сцена возвращена к CityGreybox; пользовательское городское сохранение не использовалось.
