# NotesCommander Backend FuncTests

This project contains functional and integration tests for the NotesCommander backend API.

## Test Categories

### Functional Tests
Tests that require external dependencies like Docker containers or the full application stack.

#### BackendWhisperIntegrationTests
- **Purpose**: Integration tests through the backend API using Aspire
- **Requirements**: Docker running locally
- **Approach**: 
  - Starts the entire application stack via Aspire (AppHost)
  - Discovers backend service endpoint automatically
  - Calls the backend `/api/whisper/transcribe` endpoint
  - Tests the full integration path: Backend → WhisperClient → Whisper Container
- **Run**: `dotnet test --filter "Category=Functional&FullyQualifiedName~BackendWhisper"`
- **Benefits**:
  - Tests the actual production code path
  - Validates service discovery and configuration
  - Ensures backend API contract is working correctly

## Running Tests

### Prerequisites
- Docker Desktop installed and running
- .NET 10 SDK installed

### Run All Tests
```bash
dotnet test
```

### Run Only Functional Tests
```bash
dotnet test --filter Category=Functional
```

### Run Specific Test Class
```bash
# Legacy direct Whisper tests
dotnet test --filter "FullyQualifiedName~WhisperIntegrationTests"

# New backend API tests via Aspire
dotnet test --filter "FullyQualifiedName~BackendWhisperIntegrationTests"
```

## Test Data
- Test audio file: `TestData/samples_jfk.wav` (JFK's inauguration speech excerpt)
- Expected transcription fragment: "ask not what your country can do for you"

## Architecture

### BackendWhisperIntegrationTests Flow
```
Test → Aspire (AppHost) → Backend API (/api/whisper/transcribe) → WhisperClient → Whisper Container
```

The test:
1. Creates and starts the Aspire application using `DistributedApplicationTestingBuilder`
2. Gets an HttpClient configured for the `notes-backend` resource
3. Waits for the backend health endpoint to respond
4. Sends a multipart/form-data POST request with audio file to `/api/whisper/transcribe`
5. Validates the transcription response

This approach ensures we're testing the actual production code path, including:
- Service configuration and dependency injection
- HTTP endpoint routing and model binding
- File upload handling via MediaStorage
- WhisperClient integration with the Whisper container
- Error handling and response formatting


Этот проект содержит тесты для бэкенда NotesCommander.

## Типы тестов

### Unit Tests (Модульные тесты)
Быстрые тесты, которые не требуют внешних зависимостей (Docker, сеть и т.д.).

**Запуск:**
```bash
dotnet test --filter "Category!=Functional"
```

### Functional Tests (Функциональные тесты)
Тесты, которые проверяют интеграцию с внешними сервисами (например, Whisper в Docker).
Эти тесты требуют:
- Установленный Docker Desktop (или Docker Engine) **и Docker должен быть запущен**
- Доступ к интернету для загрузки Docker образов
- Больше времени на выполнение

**Запуск:**
```bash
# Убедитесь, что Docker запущен!
dotnet test --filter "Category=Functional"
```

**Запуск всех тестов (включая функциональные):**
```bash
dotnet test
```

**Примечание:** Если Docker не запущен, функциональные тесты будут пропущены с ошибкой "Docker is either not running or misconfigured".

## Функциональные тесты Whisper

### WhisperIntegrationTests

Этот тест проверяет полную интеграцию с сервисом распознавания речи Whisper:

1. **Автоматически запускает Docker контейнер** с Whisper (`fedirz/faster-whisper-server:latest-cpu`)
2. **Использует реальный аудиофайл** (`TestData/samples_jfk.wav`) с фрагментом речи JFK: "Ask not what your country can do for you..."
3. **Отправляет аудио на распознавание** через WhisperClient
4. **Проверяет корректность распознавания** - что транскрипция содержит ожидаемый текст
5. **Автоматически останавливает и удаляет контейнер** после теста

### Что проверяется:

- ✅ Whisper контейнер успешно запускается через TestContainers
- ✅ WhisperClient корректно формирует HTTP запросы
- ✅ Аудиофайл успешно отправляется и обрабатывается
- ✅ Ответ от Whisper корректно десериализуется (включая Text, Language, Duration)
- ✅ Обработка ошибок (несуществующий файл)
- ✅ Автоматическая очистка ресурсов (контейнер останавливается и удаляется)

### Первый запуск

При первом запуске функциональных тестов:
1. Docker загрузит образ `fedirz/faster-whisper-server:latest-cpu` (~2-3 GB)
2. Whisper загрузит модель `tiny` (~75 MB)
3. Это может занять несколько минут

Последующие запуски будут значительно быстрее, так как образ и модель уже будут в кэше.

### Используемая модель

Для тестов используется модель **tiny** вместо **base**, чтобы:
- Ускорить загрузку модели
- Ускорить выполнение тестов
- Уменьшить использование памяти

В production коде используется модель **base** для лучшего качества распознавания.

## Структура проекта

```
NotesCommander.Backend.FuncTests/
├── Services/
│   └── WhisperIntegrationTests.cs    # Функциональные тесты Whisper
├── Storage/
│   └── NoteStoreTests.cs             # Unit тесты для NoteStore
├── TestData/
│   └── samples_jfk.wav               # Реальный аудиофайл для тестирования (JFK речь)
├── FunctionalTestAttribute.cs        # Атрибут для маркировки функциональных тестов
├── XunitLoggerProvider.cs            # Logger для вывода в xUnit
└── README.md                         # Этот файл
```

## Troubleshooting

### Docker не запущен
```
Error: Cannot connect to Docker daemon
```
**Решение:** Запустите Docker Desktop

### Порт уже занят
```
Error: Port 8000 is already in use
```
**Решение:** Остановите другие контейнеры или приложения, использующие порт 8000

### Timeout при запуске контейнера
```
Error: Container did not become healthy in time
```
**Решение:** 
- Проверьте подключение к интернету
- Увеличьте timeout в тесте
- Проверьте логи Docker: `docker logs <container_id>`

## CI/CD Integration

Для интеграции в CI/CD pipeline рекомендуется:

1. **Разделить запуск тестов:**
   ```yaml
   # Unit tests - быстрые, запускаются всегда
   - run: dotnet test --filter "Category!=Functional"
   
   # Functional tests - медленные, запускаются по расписанию или вручную
   - run: dotnet test --filter "Category=Functional"
   ```

2. **Использовать Docker-in-Docker** для CI окружений (GitHub Actions, GitLab CI и т.д.)

3. **Кэшировать Docker образы** для ускорения CI builds
