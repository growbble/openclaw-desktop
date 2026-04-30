# OpenClaw Desktop

Native Windows-приложение в стиле Telegram Desktop для общения с ИИ-агентом через OpenClaw Gateway.

## Возможности

- **Telegram-подобный интерфейс**: список сессий слева, чат справа
- **Постоянные сессии**: история сохраняется локально в `%LOCALAPPDATA%/OpenClawClient/sessions/`
- **Отправка и получение файлов**: как вложение в сообщения (аналог Telegram media)
- **Inline-кнопки**: агент может отправлять кнопки быстрых действий
- **Автозагрузка медиа**: изображения и документы скачиваются автоматически в выбранную папку
- **Прогресс-бары**: асинхронная загрузка файлов с отображением прогресса
- **Системные уведомления**: Toast-уведомления Windows о новых сообщениях и файлах
- **Статус доставки**: галочки ⏳✓✓✓✓ как в Telegram
- **Indicate "печатает..."**: показывается пока агент формирует ответ
- **Streaming**: ответы от агента приходят потоком
- **Настройки**: API endpoint, токен, папка загрузок, автоскачивание, уведомления

## Архитектура

```
OpenClawClient/
├── App.xaml/cs                  — точка входа, глобальный DI
├── Package.appxmanifest         — манифест пакета
├── Models/
│   ├── AppConfig.cs             — конфигурация приложения
│   ├── ChatSession.cs           — сессия чата (аналог чата в Telegram)
│   ├── ChatMessage.cs           — сообщение с вложениями и кнопками
│   ├── Attachment.cs            — файловое вложение (media в Telegram)
│   ├── InlineButton.cs          — inline-кнопка
│   ├── AgentResponse.cs         — ответ от агента (текст + вложения + кнопки)
│   ├── OpenAIModels.cs          — DTO для OpenAI API /v1/chat/completions
│   └── ReceivedFile.cs          — скачанный файл
├── Services/
│   ├── ConfigService.cs         — загрузка/сохранение настроек JSON
│   ├── SessionService.cs        — управление сессиями
│   ├── OpenClawGatewayService.cs — HTTP-клиент для Gateway (IOPenClawGatewayService)
│   ├── FileDownloadService.cs   — автоскачивание файлов из ответов
│   ├── PollingService.cs        — фоновый polling каждые 2-3 сек
│   └── NotificationService.cs   — Toast-уведомления Windows
├── ViewModels/
│   └── MainViewModel.cs         — бизнес-логика UI
├── Views/
│   ├── MainWindow.xaml/cs       — главное окно
│   ├── MainChatPage.xaml/cs     — страница чата (дизайн Telegram Desktop)
│   └── SettingsPage.xaml/cs     — страница настроек
├── Converters/
│   └── BoolConverters.cs        — конвертеры для XAML-биндинга
└── Assets/
    └── icon.ico                 — иконка приложения
```

## Требования

- Windows 10 (1809+) или Windows 11
- .NET 8 SDK + Windows App SDK 1.6
- Visual Studio 2022 с рабочей нагрузкой "Разработка классических приложений на .NET"

## Сборка

```powershell
# Открыть решение
OpenClawClient.sln

# Или через CLI
dotnet build -c Release
```

## Настройка Gateway на VDS

В `config.yaml` Gateway должно быть включено:

```yaml
gateway:
  http:
    endpoints:
      chatCompletions:
        enabled: true
      responses:
        enabled: true  # нужно для отправки файлов
```

## Первый запуск

1. Указать URL сервера: `http://ваш-vds:18789`
2. Ввести Gateway Token (из конфига `gateway.auth.token`)
3. Выбрать папку для загрузок (по умолчанию `%USERPROFILE%/Downloads/OpenClaw`)
4. Нажать "Проверить соединение"
5. Готово! Можно общаться.

## API-эндпоинты

- `POST /v1/chat/completions` — отправка сообщений (OpenAI-совместимый)
- `POST /v1/responses` — отправка файлов (OpenResponses API)
- `GET /v1/models` — проверка соединения
- Аутентификация: `Authorization: Bearer <token>`
