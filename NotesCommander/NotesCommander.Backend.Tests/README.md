# NotesCommander Backend Tests

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
NotesCommander.Backend.Tests/
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

