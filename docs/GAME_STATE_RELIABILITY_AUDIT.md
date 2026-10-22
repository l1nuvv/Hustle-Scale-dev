# Аудит надёжности игрового состояния — 22.10.2026

## A. Вердикт и область доказательств

Исходный checkout: `d6473fde1dac4806a28310daede4c3c4575fa1f8`, ветка `codex/recover-prototype`, рабочее дерево было чистым. Исправление: `7683426`; актуализация проверок: `e9ffedd`. Unity Editor **6000.6.0f1 (f7f8ed4d1e24)**, Windows, MCP, Edit Mode domain harness и реальные Play Mode-сцены. Проверялись исходный HEAD и перечисленные изменения; локальные журналы относятся к этому проходу, а не к историческим PASS.

В охваченных городских и FIRST DEAL-последовательностях нарушения денег, ID, оплаты, ремонта и восстановления не обнаружены. Найдены и исправлены **два P0-дефекта legacy-подбора**: потеря предмета при полном инвентаре и дублирование при двух вызовах до deferred Destroy. Их область — `ItemPickup`/слоты старого `SampleScene`, а не городской реестр фонарей. Дополнительно закрыта невалидная величина legacy-стека и исправлены устаревшие ожидания harness’ов. Подтверждённых незакрытых P0/P1 для обычных проверенных действий игрока нет.

Это ограниченный технический аудит, не математическое доказательство всех возможных последовательностей. Ручного прохождения человеком и нового Player build не было. E6.6 остаётся открытой; приёмка E4/E5 не пересматривается. Правила и числа из [FIRST_DEAL](FIRST_DEAL.md) и [CITY_GAMEPLAY](CITY_GAMEPLAY.md) не изменены.

## B. Владельцы состояния

| Данные | Владелец и пути записи | Читатели / жизненный цикл |
| --- | --- | --- |
| Кошелёк, расходы, выручка, батареи | `WorkshopSession` / `WorkshopOutcome`; purchase, battery, delivery, buyback | View/HUD читают; новой сессии — новый кошелёк |
| Три активных экземпляра | `FlashlightUnit[]` внутри session; ID, initial/current condition, location, diagnosis, power | Мир представляет ID через три трансформа; не владеет отдельным продаваемым товаром |
| Первая городская партия после restock | `previousUnits` / `previousOrders` той же session | Только история; старые ID не входят в текущую передачу |
| Заказы, время, прогресс | `WorkshopTrading`, `WorkshopOutcome` | Фиксированные NPC — точки взаимодействия и реакции, без отдельного кошелька/товарного склада |
| Городская session и её замена | `CitySaveController`: NewGame / validated Load / Bind | `SessionReplaced` переподключает View; старые Changed/TransactionCompleted снимаются |
| Положение мира | `CityWorldData`; View.BeforeSave, player/vehicle poses | Save содержит значения, не Unity object references |
| Файлы | `CitySaveStore`: JSON envelope, checksum, tmp, replace, backup | Восстановление создаёт отдельный кандидат до замены живых ссылок |
| FIRST DEAL | `WorkshopView.Awake` создаёт session; `FirstDealView` управляет меню и часами | Reload сцены создаёт новую session; постоянного save этого fixture нет |
| Legacy Item / слоты | `InventoryManager`, `InventorySlot`; `ItemPickup` и запасной путь `PlayerRaycast` | Отдельный старый ассортимент; не второй реестр торговых фонарей |

`Item`/`ItemFood` ScriptableObject содержат описание товара; индивидуальное состояние фонарей в них не записывается. Runtime static singleton `Inventory.Instance` относится к старому, не подключённому к текущей торговле классу. Общего GameManager/Wallet/Clock-сервиса нет и для аудита не добавлено. Сеттеры экземпляров internal: разделение владельцев поддерживается текущими путями вызовов, а не отдельными asmdef.

## C. Проверенные инварианты

| Инвариант | Свежие доказательства |
| --- | --- |
| `InitialBalance + Revenue - Expenses == Balance`, баланс неотрицателен | T06 после действий; новые city sequences; T04/T05, E6 и save validator |
| Покупка партии однократна; отказ атомарен; денег хватает до списания | T04 границы $179/$180; T06; E6 запрещает третью партию и замену непроданного товара |
| Одна продажа — одна оплата и окончательная передача ID | T04/T05, S3, E6; повторы закрытых заказов/старых ID, duplicate и mixed-invalid группы |
| ID уникальны и стабильны до restock; новая партия имеет новые ID | T06, E6, S3; v4 хранит обе партии, валидатор исключает общие ID |
| Отказ передачи не изымает часть группы | T04, S3, snapshots в E6 / city sequences |
| Не более одного фонаря в руках/на верстаке; ящик и фонарь не делят руки | T06; city snapshots валидируются после каждого действия; S3/E5.7 |
| Диагноз принадлежит экземпляру; F не подменяет тест | T04/T05; S3 реальный F; E5.7 проверяет light/collider/visibility после restore |
| Repair расходует одну батарею, прибавляет 240 секунд и сбрасывает тест | T04, T05, S2, E6, E5.7; повторный repair отвергается |
| Без повторного теста repaired lamp нельзя передать | T04/T05 и сохранение repaired-untested состояния в E5.7 |
| Срок строгий; пауза не тикает; отмена не платит | T04 до/ровно/после границы, S2 fractional deadline/repair jump, S3 модальное меню |
| После финала FIRST DEAL нет новых экономических действий | T06 frozen-state checks для каждого seed; T05 завершение и reload |
| Load не оплачивает заказ повторно и не меняет состав купленной партии | S2/E6 round trips; S3/E5.7 повторные Load и отказ повторной передачи |
| Повреждённый Load сохраняет живые session/world и хороший резерв | S2 файловые отказы; E5.7 живой runtime и corrupt startup |
| Legacy-подбор либо полностью принят, либо остаётся в мире | Новый Play Mode harness: RED до правки, GREEN после; 17 утверждений |

City sequences: 32 фиксированных seed × 256 действий, в том числе перенос, карман, мир, свет, багажник, заказы, время, пауза и восстановление. После каждого действия — восстановимость snapshot, его точное соответствие кандидату и учёт денег; после отказа — неизменность snapshot и отсутствие событий. Для операций проверяется число Changed/TransactionCompleted. Будущий RNG после тестового restore явно seed’ится существующим `S2Verification.SeedCity`; купленные состояния не меняются. Это тестовая фиксация случайности, не изменение production RNG.

## D. Транзакции и отказы

Все доменные операции синхронны, без промежуточного coroutine ремонта или отложенного начисления денег. Commit — группа присваиваний после проверки условий; затем `Complete` вызывает Changed и TransactionCompleted. Между проверкой группы товаров и её передачей нет yield/await.

| Операция | Условия / commit | Отказ, повтор и представление |
| --- | --- | --- |
| PurchaseLot | Режим, пауза, лимит партий, оставшийся запас и деньги; затем ID/товар, расходы, баланс, счётчик | Ничего не списывает при отказе. Городская View дополнительно проверяет пустой ящик у продавца. Повтор не создаёт товар |
| BuyBattery | Не завершено, допустимая пауза, $50 и предел stock | Расход и батарея вместе; повторная отдельная покупка разрешена, пока хватает денег |
| Перенос / подбор / постановка | Владение, занятость рук/стола, location; View проверяет reach, препятствия и поверхность | Сохраняются диагноз и ID. Toggle-действия могут закономерно переключаться обратно; это не дублирование |
| Test / Repair | Свой предмет на столе; для repair подтверждённый дефект и батарея | Test не чинит. Repair одноразов для данного дефекта; после него требуется новый тест |
| Accept / Close | Доступное предложение или принятый заказ; предел срока | Нет движения денег/товара. Close окончателен. FIRST DEAL допускает управление заказами из паузы меню |
| Deliver / SellBack | Вся группа: число, уникальность, принадлежность, физическое предъявление, тест/качество; для заказа статус/срок; предел кошелька | После проверки всей группы — location, баланс, ledger, статус, пересчёт оставшихся предложений. Старый ID и закрытый заказ не платят |
| Time / expiry | Конечный неотрицательный delta, старт, отсутствие паузы/финала | Просрочка меняет заказ, не выдаёт выплату. Invalid delta бросает ArgumentOutOfRangeException без изменения состояния |
| EndShift / Restart | Только FIRST DEAL; подтверждение отказа при отсутствии основной продажи | Финал замораживает session; reload создаёт новый набор ID/$300/intro |
| Save / Load | Не за рулём; полная валидация снимка/файла до Bind | Ошибка сохраняет текущую session и сообщение; файлы заменяются через tmp, хороший backup защищён |
| Legacy pickup | Теперь `InventoryManager.AddItem` возвращает bool и валидирует quantity/capacity | Только success потребляет world object; `consumed` и немедленный SetActive(false) защищают deferred Destroy. Raycast-слот также удаляется только после success |

UI не вычисляет выплату и не записывает баланс. Городской Execute временно снимает паузу для одной команды и восстанавливает её в finally. Refresh читает актуальную session; изменённый ранее механизм подавления повторного rebuild не менялся. Проданные FIRST DEAL-объекты могут завершать косметическую анимацию: коллайдеры выключаются сразу, при финале визуал окончательно скрывается.

## E. Таблицы переходов

| Система | Переход | Условия / последствия |
| --- | --- | --- |
| Order | Available → Accepted | FIRST DEAL: фиксированный срок 540/1080; город: elapsed + 540/1080 |
| Order | Available → Declined; Accepted → Cancelled | Явное действие, без денег/товаров |
| Order | Available → Expired; Accepted → Failed | `elapsed >= deadline`; первый переход применяется только к FIRST DEAL |
| Order | Available/Accepted → Unavailable | Оставшийся свой запас меньше quantity; покупка не возрождает закрытый заказ |
| Order | Accepted → Completed | Полная валидная передача и единственная выплата |
| RepeatB | Locked → Available → Accepted → Completed | Только FIRST DEAL после первой продажи Б; тот же срок 1080; нехватка остатка делает Unavailable |
| City restock | Первая исчерпанная партия → вторая | Новые экземпляры и A/B, прежние объекты сохраняются в истории; третьей партии нет |
| Lamp | Seller → Crate → Hands/Bench/Pocket/World | Единственный Location определяет владение; back-and-forth перенос ID не меняет |
| Lamp | Hands/переносимый Crate → BuyerA/BuyerB/BoughtBack | Терминальное владение; тот же ID нельзя вернуть в запас продажей/подбором |
| Diagnosis | Unknown → TestedWorking/TestedFault | Только Test на столе |
| Repair | TestedFault → WorkingUnknown → TestedWorking | Батарея −1, +240; повторный тест обязателен |
| Crate | Dock/World ↔ Carried ↔ Vehicle | Проверки занятости и остановки машины; содержимое имеет Location.Crate |
| Session | Fresh → Active/Paused → Finished | Finished только FIRST DEAL; город не имеет общей смены/автофинала |
| Restore | Valid DTO → новая Paused session | Не вызывает сделки/выплаты; невалидный кандидат не заменяет текущий |

ID заказа сейчас не GUID: `DealOrderId` вместе с контекстом партии/session определяет A/B; RepeatB имеет отдельный enum. Межпартийных ссылок по одному enum в текущей передаче нет. Не добавлялись новые идентификаторы или state-machine framework.

## F–H. Время, Unity lifecycle, save/restart

Источник elapsed — session. `CitySaveController` и `FirstDealView` тикают раньше `PlayerInput` (-200 против -100), поэтому игровая передача видит уже обновлённую границу кадра. UI-команды из модального меню выполняются при замороженном чтении времени. Repair прибавляет 240 сразу; ожидания четырёх реальных минут нет. Coroutines в fixture обслуживают feedback, а не владение или оплату. Реальная пауза/меню, строгая граница, fractional round trip и попадание repair ровно на дедлайн покрыты T04/S2/S3/E5.7.

`EditorSettings`: EnterPlayModeOptionsEnabled=1, options=0 — отключённых Domain/Scene Reload нет. Не объявляется баг очистки static при настройках, которые проект не использует. Подписки PlayerRaycast и WorkshopView парные OnEnable/OnDisable; городской Bind снимает прежние Changed, контроллер — TransactionCompleted; OnDestroy снимает input/SessionReplaced/BeforeSave и удаляет runtime presentation. E5.7 повторяет загрузку и запуск сцены, проверяет одну композицию и три targets, состояние игрока/машины/мира и старые ссылки. Ошибок обычного порядка Awake/Start в этих прогонах не было.

Read-only инспекция через Editor: **7 сцен, 16 prefab’ов, 157 компонентов проекта**, missing scripts=0, broken assigned serialized references=0. Проверялись реальные загруженные assets, включая SampleScene и Slot/AppleRed. Это не проверка всех необязательных null-полей или всех сторонних ассетов. Ни сцены, ни prefab YAML, ни GUID существующих assets не менялись.

City payload v4 и чтение v1/v2 проверены S2/E6. Для v3 отдельно через MCP проверены реальные envelope-файлы двух составов и обеих партий: точные ID/condition/ledger/history после загрузки. Backup, checksum, truncated JSON, семантически неверный snapshot, file lock, oversized file и ошибка директории проверены; E5.7 подтверждает сохранение живых ссылок и восстановление после corrupt startup. Состояние repaired-untested/карман/поставленный включённый фонарь/принятый заказ загружено после реального scene restart и затем продано без второй оплаты. За рулём save/load/new game запрещены. FIRST DEAL save не реализован; его restart реально прошёл в T05, несмотря на выключенную build-сцену. Гипотеза о неисправном restart опровергнута в Editor; отдельный FIRST DEAL Player не собирался.

## I–J. Воспроизведения и исправления

**GS-01 — P0, потеря legacy-предмета при отказе.** Файлы: `InventoryManager.cs`, `ItemPickup.cs`, `PlayerRaycast.cs`. В prefab-слоте яблоко ×10/max10, свободных слотов нет; вызвать PickupItem либо raycast-перенос world slot ×1. Ожидание: отказ, оба запаса прежние, источник остаётся активным. До правки: AddItem молча возвращал void, вызывающий код сообщал успех и удалял источник в конце кадра. Нарушение — сохранение количества и атомарность отказа. Исправление: bool-результат и удаление только после приёма. Regression: full inventory, следующий кадр, retry после освобождения места, оба пути. **RED → GREEN наблюдались.**

**GS-02 — P0, дублирование legacy-подбора.** Файл: `ItemPickup.cs`. Пустой слот; два PickupItem одного объекта в одном кадре. Ожидание: одна единица и второй false. До правки: два успешных добавления до deferred Destroy. Причина — отсутствующий флаг потребления и активный источник до конца кадра. Исправление: consumed + немедленная деактивация после успешного AddItem. Regression: два вызова, проверка quantity в том же/следующем кадре, cached retry. **RED → GREEN наблюдались.**

**GS-03 — P2, невалидная величина legacy-стека.** Файл: `InventoryManager.cs`. В пустой слот передать −1, 0 или 11 при maxStack=10. До правки создавался отрицательный/нулевой/переполненный стек. Теперь весь запрос отвергается; проверка вместимости существующего стека использует вычитание вместо потенциально переполняющейся суммы. Это валидация той же границы приёма, без добавления split-stack механики. **RED → GREEN наблюдались.**

**GS-04 — P2, устаревшие проверки T04/T05.** Проверки искали отказ/выкуп в Terms вместо Trade feedback, деньги — в подсказке вместо HUD, а в паузе ожидали немедленное окончание анимации. T04 также требовал старый текст общей причины Unavailable. Производственные статусы/деньги при этих падениях были корректны. Обновлены именно поверхности и актуальные причины, сохранены проверки оплаты, stock, финального скрытия, коллайдеров, disabled-кнопок, shortage и повторов. Добавлен `Tools/T04/Verify Trade Domain` для узкого повторного запуска. **T04 domain / T05 стали PASS; исходные ошибки сохранены в Temp/StateAudit.** Полный T04 Play Mode здесь не запускался и не объявляется пройденным.

## K. Результаты и воспроизводимые команды

Все команды ниже — меню открытого Unity Editor, завершившиеся в этом аудите. Числа — **assertions**, не число NUnit tests, маршрутов или игроков. Некоторые интеграционные harness’ы считают frame/timeout-проверки, поэтому количество зависит от длительности запуска.

| Команда / уровень | Результат | Полный локальный журнал относительно корня |
| --- | --- | --- |
| `Tools/T06/Verify Invariants and Routes` / Edit Mode domain | PASS 222310; 64×256 действий, 7 маршрутов | `Logs/Codex-T06-verification.log` |
| `Tools/T04/Verify Trade Domain` / Edit Mode domain | PASS 74 | `Logs/Codex-T04-domain.log` |
| `Tools/T05/Verify Outcomes` / domain + Play Mode | PASS; 88 PASS-записей | `Logs/Codex-T05-verification.log` |
| `Tools/S2/Verify City Save and Load` / Edit Mode | PASS 235 | `Logs/Codex-S2-verification.log` |
| `Tools/E6/Verify Restock and RNG` / Edit Mode | PASS 8643; 128 seeds, оба лота и все 8 составов | `Logs/Codex-E6-domain.log` |
| `Tools/E6/Verify Second Trading Cycle in Play Mode` | PASS 282 | `Logs/Codex-E6-playmode.log` |
| `Tools/S3/Verify City Trading in Play Mode` | PASS 39750 | `Logs/Codex-S3-playmode.log` |
| `Tools/E5/Verify Slice Persistence` / Play Mode, restart | PASS 864 | `Logs/Codex-E5.7-playmode.log` |
| `Tools/Quality/Verify City State Sequences` / Edit Mode | PASS 32457; 32×256 действий | `Logs/Codex-city-state-sequences.log` |
| `Tools/Quality/Verify Pickup Integrity in Play Mode` | PASS 17; до fix было FAIL по потере/duplication/quantity/raycast | `Logs/Codex-pickup-reliability.log`; RED: `Temp/StateAudit/Pickup-before-fix.log` |
| Read-only сцены/prefab’ы / Editor | 7/16, 0 missing / 0 broken assigned refs | `Logs/Codex-state-audit-references.log` |
| v3 envelope round trip / MCP, Edit Mode | PASS 24; 2 seeds × 2 партии, с историей | `Logs/Codex-state-audit-v3.log` |
| Исключение Changed / отдельная неперсистентная session | Commit цел, subsequent notification пропущено — см. L | `Logs/Codex-state-audit-faults.log` |

Срезы запусков: T06/S2/E6 domain — исходный HEAD; E6 Play Mode — тот же runtime с начатой правкой T05. S3/E5.7/T05/pickup/city sequences — рабочие версии исправления `7683426` и тестов, до их коммита; T04 domain — окончательная правка, вошедшая в `e9ffedd`. В более поздних правках тестового файла менялись только дополнительные проверки/seed будущего RNG; runtime после успешных S3/E5.7 не менялся. v3 и fault injection выполнены на `e9ffedd`. Исходные RED-логи сохранены отдельно; поздний GREEN не стирает факт обнаруженного дефекта.

Unity скрипты скомпилированы; после завершения refresh/readiness ошибок Console=0. Существуют CS0618-предупреждения старых FindObject/FindObjectsSortMode API; миграция API вне исправления не выполнялась. Полный Editor log скопирован в `Logs/Codex-state-audit-editor.log`. Неудачная первоначальная CLI-попытка не запускала тестов (`Logs/Audit-T06-editor.log`, exit 1); результаты получены через MCP работающего Editor. Не смешивать её с успешным T06.

Test Runner обнаружил только корневые EditMode/PlayMode suites: project NUnit/UnityTest cases и asmdef отсутствуют. NUnit-проходы не заявляются. Форматирование dotnet whitespace завершилось exit 0; git diff --check и focused review выполнены перед коммитами. Логи/снимки/тестовые saves не включаются в Git. Игровые saves человека не использовались: городской harness задаёт отдельные пути в Temp, pickup проверяется в пустой временной сцене.

FIRST DEAL domain-маршруты: A+выкуп=$415, A+B=$455, B+A=$455, B+RepeatB+выкуп=$390, fallback=$275, repair+fallback=$250, abandon=$120. T04 отдельно проверяет передачу за 0.001 секунды до срока, точно на сроке и после него, независимую просрочку, repair jump на 540, invalid delta. T05 проходит резервный выкуп, отказ от продолжения, restart, продажу Б, конкурирующий А, исчерпание RepeatB, второй restart и repair/retest в сцене. Это автоматизированное прохождение с реальными компонентами и вводом в части шагов; не ручная попытка человека. Городские повторные A/B относятся к новой партии; RepeatB в город не добавлен.

## L. Оставшиеся риски и очередь

1. **P2, GS-R01: исключения наблюдателей не изолированы.** Подтверждено fault injection: добавить к Changed обработчик, бросающий InvalidOperationException, затем PurchaseLot. Ожидаемая устойчивость уведомлений — сообщить об ошибке представления и всё равно запросить сохранение завершённой сделки. Факт: баланс $120, 3 owned, но TransactionCompleted не вызван и вызов операции бросает исключение. Причина: синхронный Changed предшествует TransactionCompleted без изоляции. Деньги и товар согласованы; обычного игрового пути, вызывающего такое исключение, не найдено. Не выдаётся за обнаруженную потерю денег или runtime P1. Следующая техническая задача — определить контракт ошибок подписчиков и добавить regression на него, если решено поддерживать такую изоляцию; произвольное проглатывание исключений здесь не внедрялось.
2. **P2/P3, GS-R02: неиспользуемый `Inventory.cs`.** Статически: AddItem создаёт InventorySlot без заполнения item/amount и не учитывает весь amount при stacking; public singleton/slots допускают внешние записи. Ссылок этого компонента в проверенных сценах/prefab’ах и production-вызовов этого AddItem не найдено. Это dormant риск, не воспроизведённый блокер города. Перед повторным использованием старого API — отдельный контракт и regression; не подключать его к торговым фонарям автоматически.
3. **Границы проверки:** не выполнялись power-loss/kill процесса во время записи, произвольное внешнее уничтожение внутренних GameObject между callback’ами, длительная soak-сессия, отключённые Domain/Scene Reload и новый Player build. Проверка main/tmp/backup при файловом отказе и реальный scene reload выполнены; они не заменяют перечисленные эксперименты.
4. **Следующая человеческая проверка:** E6.6 по [PLAYTEST](PLAYTEST.md), включая самостоятельное понимание второй партии. Нового участника, приёмки или оценки баланса аудит не создаёт. Будущий RNG ещё не купленной партии не сохраняется; уже купленный состав стабилен. Изменение этой политики и экономики требует отдельного решения владельца.

## M. Git и изменённые файлы

- `7683426 fix(inventory): make legacy pickup transfers atomic` — ItemPickup, InventoryManager, PlayerRaycast; новый GameStateReliabilityVerification и его Unity .meta. Производственная правка касается только границы legacy-приёма/потребления; новая проверка также покрывает городские последовательности.
- `e9ffedd test(gameplay): refresh reliability harness expectations` — T04Verification и T05Verification, без изменений геймдизайна/UI production.
- Этот отчёт и ссылка в DEVELOPMENT фиксируются отдельным документационным коммитом. Его hash доступен через `git log`; самоссылочный hash в текст не встраивается.

Ни branch switch, ни reset, ни push, ни package/Unity upgrade, ни сборка/ZIP не выполнялись. После тестов Editor оставлен вне Play Mode на CityGreybox. Статус дерева проверяется после финального документационного коммита; посторонних исходных dirty paths не было.
