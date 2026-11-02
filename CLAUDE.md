# MouseHighlighter

Windows tray app (C# / WPF, .NET 10) that highlights the mouse cursor and shows click ripples. UI text, comments and docs are in Korean — keep them Korean.

## Read first
- `README.md` — what the app does, folder layout
- `docs/구조.md` — architecture, data flow, design decisions, full settings table
- `docs/수정-가이드.md` — step-by-step recipes (e.g. adding a slider setting), rules, troubleshooting

## Commands
- Run for development: `dotnet run --project MouseHighlighter.csproj -- --settings` (opens the settings window on start; only one instance can run — a named mutex blocks a second one)
- Build the shareable exe: `dotnet publish MouseHighlighter.csproj -p:PublishProfile=Share`, or run `exe 다시 만들기.bat`. This produces a self-contained single-file `MouseHighlighter.exe` at the project root (built in `bin\share\`, then copied by the `CopyExeToRoot` target).
- Kill a running instance before publishing (the exe is locked otherwise): `taskkill /im MouseHighlighter.exe /f`

## Rules that are easy to break
- Never read files next to the exe. It ships as a single file, and `Assembly.Location` is empty there. Embed resources instead; persist only to `%AppData%\MouseHighlighter\settings.json`.
- Don't set `PublishDir` to the project folder itself. The SDK then excludes all sources (error CS5001).
- Overlay windows must derive from `OverlayWindow` and be positioned with `NativeMethods.CenterWindowAt` (physical pixels, PerMonitorV2). Don't use `Left`/`Top`.
- Any `CompositionTarget.Rendering` subscription must be removed when its effect is off, or idle CPU stays at ~3%.
- On/off decisions live only in `App.UpdateActiveState()`. After changing `Settings.Current`, call `Settings.NotifyChanged()`.
- Keep the low-level mouse hook callback fast. A slow callback lags the mouse system-wide.
