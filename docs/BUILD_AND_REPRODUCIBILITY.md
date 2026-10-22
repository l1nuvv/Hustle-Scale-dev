# Сборка и воспроизводимость Hustle & Scale

Проверяемый workflow Windows x64, 22.10.2026. Результаты конкретных запусков и границы доказательств — в [аудите](BUILD_REPRODUCIBILITY_AUDIT.md). Функциональная воспроизводимость не означает побайтовое совпадение Unity Player.

## Окружение

- **Unity 6000.6.0f1, revision f7f8ed4d1e24**, как в `ProjectSettings/ProjectVersion.txt`. Установить эту версию через Unity Hub и активировать подходящую лицензию; проверка выполнена с Unity Personal. Другую Unity автоматически не выбирать.
- Windows x64; проверено на Windows 11. Нужен установленный Windows Build Support, включая поставляемый с Editor Mono Player. Проект использует **Mono2x**, stripping Disabled, API enum `NET_Standard_2_0` (название Unity; Standard profile .NET Standard 2.1). IL2CPP/MSVC не нужны для проверенной Mono-сборки. Переход на IL2CPP — отдельная задача.
- Git и Git LFS в PATH. Проверены Git 2.54.0.windows.1 и Git LFS 3.7.1. Submodules сейчас отсутствуют. Без LFS checkout вместо моделей/текстур может содержать pointers.
- Сеть к GitHub, Git LFS storage, `packages.unity.com` и `download.packages.unity.com`. Unity MCP закреплён Git SHA `d49ae2953580f3481beb1e084a1da2682f0b5610`; registry-версии и транзитивные зависимости определяет lock. Первый импорт не является offline workflow.
- Rider — основная IDE владельца, но для сборки не требуется. .NET SDK/Git Bash нужны локальному formatting hook при коммитах C#, не заменяют встроенный Unity toolchain. Blender нужен только для правки SourceArt; FBX уже отслеживается.

Unity, manifest/lock, URP 17.6.0, Input System 1.20.1 и Player Settings не обновлять ради сборки. Собственных native DLL, local/file packages, custom registries и runtime AssetBundles/Addressables в проверенном маршруте нет. Лицензия Unity/сетевые credentials не должны попадать в Git.

## Чистый clone

PowerShell, новая директория:

```powershell
git clone --depth 1 --single-branch --branch dev https://github.com/l1nuvv/Hustle-Scale-dev.git Hustle-Scale-dev
if ($LASTEXITCODE -ne 0) { throw 'Clone failed; inspect Git/LFS output.' }
Set-Location Hustle-Scale-dev
git rev-parse HEAD
git status --short
git lfs fsck
```

`clone` с установленным LFS восстанавливает LFS content автоматически. При checkout с `GIT_LFS_SKIP_SMUDGE` выполнить `git lfs pull` и проверить hashes; этот отдельный skip-smudge маршрут в аудите не запускался. Не переносить исходный `Library`, `Temp`, `UserSettings`, `.idea` или IDE-generated проекты. `Assets/Resources.meta` сопровождает пустую папку: Git не хранит пустые каталоги, Unity восстанавливает её при импорте.

SHA local HEAD и remote могут отличаться. Перед сравнением записать `git rev-parse HEAD` и `git ls-remote origin refs/heads/dev`. Новые локальные коммиты появятся в remote только после отдельного разрешённого push. Этот документ и новый entry point могут ещё отсутствовать в опубликованном SHA; для старого checkout доступна штатная production-команда ниже.

## Запуск Unity из PowerShell

Windows GUI executable нужно явно дождаться. Следующая оболочка использует тот же `Start-Process`/quoting/WaitForExit подход, что проверочные запуски, в том числе для путей с пробелами:

```powershell
$project = (Get-Location).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
New-Item -ItemType Directory -Path Logs, Builds -Force | Out-Null
function Invoke-Unity([string[]]$UnityArgs) {
    $process = Start-Process -FilePath $unity -ArgumentList (
        $UnityArgs | ForEach-Object { '"' + $_ + '"' }
    ) -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exit $($process.ExitCode); inspect -logFile." }
}
```

Проверочный checkout должен быть закрыт в других Editor-процессах. Исходный рабочий проект закрывать или очищать не требуется. При открытии через Hub дождаться импорта и проверить Console; первичная компиляция без Rider — часть Unity import.

Для проверки без глобального UPM cache **до старта Unity** задать новые пути только в текущем PowerShell-процессе:

```powershell
$env:UPM_CACHE_ROOT = Join-Path $project 'Builds/IsolatedUPM'
$env:UPM_NPM_CACHE_PATH = Join-Path $env:UPM_CACHE_ROOT 'db'
$env:UPM_GIT_LFS_CACHE_PATH = Join-Path $env:UPM_CACHE_ROOT 'git-lfs'
```

Каждая независимая cold-проверка требует нового clone и нового отсутствовавшего cache root. Эти переменные не меняют глобальный cache пользователя. Bundled packages поставляются выбранным Editor. См. [Unity global cache](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/package-manager-caches/upm-config-cache).

```powershell
Invoke-Unity @('-batchmode', '-quit', '-projectPath', $project,
    '-buildTarget', 'StandaloneWindows64', '-logFile', "$project/Logs/import.log")
git status --short
```

Сохранить полный лог и exit code. Проверить отсутствие `error CS`, failed package resolution и import errors. Manifest/lock и settings после восстановления должны оставаться согласованными с Git. Предупреждения не считать compile errors. `Library/ScriptAssemblies` создаётся Unity заново; `.csproj`/локальная IDE не являются зависимостью.

## Тесты

```powershell
Invoke-Unity @('-batchmode', '-projectPath', $project,
    '-buildTarget', 'StandaloneWindows64', '-runTests', '-testPlatform', 'EditMode',
    '-testResults', "$project/Logs/editmode.xml", '-logFile', "$project/Logs/editmode.log")
```

Не добавлять `-quit` к test run: Test Framework сам завершает процесс после выполнения. В актуальном local source шесть Editor UnityTests; они входят в настоящий Play Mode и проверяют physics/input/UI на изолированных saves. Проверять XML `test-case`, passed/failed и число тестов: exit 0 с **0 tests** не доказывает прохождение suite. Отдельной PlayMode assembly сейчас нет; это не ещё шесть PlayMode test cases. В старом remote SHA тестовых assembly может не быть. Menu/Player harness assertion counts не являются NUnit test counts.

## Production и FIRST DEAL

Production: `EditorBuildSettings.scenes`, сейчас только **CityGreybox**. `FirstDealTrade` отключена намеренно; остальные сцены — fixtures. Нет обязательного перехода main menu → FIRST DEAL. Сохранённого Build Profile в исходниках нет.

Новый [WindowsPlayerBuild](../Assets/Editor/WindowsPlayerBuild.cs) использует штатный BuildPipeline, сохраняет `HustleScale.exe.build-report.json`, сообщает результат и возвращает exit 1 при ошибке. Требует абсолютный `.exe` path в отдельной папке, Windows64 target и отсутствие активного сохранённого Build Profile. Не меняет backend, Player Settings, shared scene list или файлы пользователя; повторная сборка обновляет только выбранный build output.

```powershell
Invoke-Unity @('-batchmode', '-quit', '-projectPath', $project,
    '-buildTarget', 'StandaloneWindows64', '-executeMethod', 'WindowsPlayerBuild.Build',
    '-buildOutput', "$project/Builds/Windows/HustleScale.exe", '-logFile', "$project/Logs/build.log")
```

Для отдельной FIRST DEAL fixture добавить `-firstDealFixture`, выбрать **другую** output-папку. Для диагностических Player harness добавить `-developmentBuild`; без него создаётся обычный Player. Это явные флаги entry point, не изменение project settings. BuildReport фиксирует scenes, flags, backend, API, errors/warnings, размер и время.

Штатный production build, проверенный также на старом remote checkout без нового скрипта:

```powershell
Invoke-Unity @('-batchmode', '-quit', '-projectPath', $project,
    '-buildTarget', 'StandaloneWindows64', '-buildWindows64Player', "$project/Builds/Windows/HustleScale.exe",
    '-logFile', "$project/Logs/build.log")
```

Сохранить весь output: exe, `HustleScale_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`, runtime DLL и остальные созданные Unity файлы. Один exe не является переносимой сборкой. Проверять успешный BuildReport/финальный лог и exit code, затем реальный запуск. Архивирование не требуется.

## Запуск и smoke

Для обычной игры открыть exe из полной output-папки. Production использует обычный `persistentDataPath`; New Game заменяет прогресс, поэтому технические проверки запускать с изолированными saves.

В актуальном local source обычный Player поддерживает `-lifecycle-audit=<absolute-directory>`: существующий observer выделяет отдельный save, проверяет startup и через пять секунд готовности завершает приложение. Для GUI-проверки добавить `-lifecycle-manual`; тогда закрыть окно после проверки. В старом remote SHA этого observer нет — неизвестный CLI-флаг Unity не гарантирует изоляцию или автоматический выход.

Для Development Player существующий `-city-verify=<absolute-directory> -city-verify-phase=lifecycle` проверяет реальные E/Esc, UI и reload; `prepare`, `continue`, `restore` выполняются последовательно в разных процессах с общей **новой тестовой** папкой. После prepare сохранить копию `city-save.json` как `prepare-save.json`, после continue — как `continue-save.json`. FIRST DEAL Development fixture: `-first-deal-verify=<absolute-directory>`. Эти маршруты и полный пример запуска описаны в [Player audit](PLAYER_STARTUP_LOADING_LIFECYCLE_AUDIT.md#воспроизведение-сборки-и-запуска).

Не объединять startup observer, city harness и e5-profile в одном запуске. Проверять report.result, failure, exit code и Player.log. UI/OS-ввод и человеческая приёмка E6.6 имеют отдельные границы; automation не закрывает приёмку.

## Ошибки и повторяемость

- GitHub reset/timeout, DNS и `download.packages.unity.com ETIMEDOUT` действительно встречались. Сохранить лог, проверить сеть, затем повторить. Для Git в аудите помог уменьшенный shallow clone с `git -c http.version=HTTP/1.1 -c http.lowSpeedLimit=1 -c http.lowSpeedTime=30 clone ...`. Это command-scoped настройки, не обязательная глобальная правка. Не удалять package или менять SHA ради сетевого успеха.
- `safe.directory` при local clone может указывать на старый Windows SID владельца. В аудите применялось точечное `git -c safe.directory=<verified-source> -c safe.directory=<verified-source>/.git clone ...`. Не добавлять wildcard исключения и не менять владельца файлов автоматически.
- License token warning не равен отсутствию лицензии: проверить последующие entitlement/exit сообщения. При настоящем license failure использовать Unity Hub; не переносить credentials между машинами.
- `com.unity.pipeline` без RuntimePipelineConfig предупреждает об отключении **своего** runtime. Это отдельный пакет, URP `PC_RPAsset` работает. Не создавать выдуманную конфигурацию и не заменять pipeline.
- CS0618 старого FindObjects API и legacy importer warnings — известные предупреждения. Их полный состав и ограничения записаны в аудите.

Для повторения: зафиксировать SHA/dirty paths → clone → записать отсутствие Library/cache → восстановить LFS/UPM → import/compile → tests → build → launch → второй build/launch. Повтор в уже заполненном Library проверяет incremental build, но не заменяет второй fresh checkout после исправлений. Отдельный компьютер, offline без cache, другие GPU/Windows и побайтовая идентичность этим workflow не подтверждаются.
