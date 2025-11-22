# NotesCommander - запуск с Aspire (MAUI + backend)

Кратко:
- Установи и залогинь dev-tunnel CLI: `winget install Microsoft.DevTunnel` → `devtunnel user login` (или `npm i -g @vs/dev-tunnel`).
- Запусти нужные устройства: Android эмулятор / iOS симулятор (Windows/Mac — без подготовки).
- Стартуй всё одной командой:  
  `dotnet run --project NotesCommander.AppHost/NotesCommander.AppHost.csproj`
  (Aspire поднимет whisper, backend, dev-tunnel и прокинет discovery в MAUI).
- Если запускаешь MAUI отдельно, передай URL бэкенда: `Backend__BaseUrl=https://...` (или `NOTESCOMMANDER_BACKEND_URL`).
