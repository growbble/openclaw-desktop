# OpenClaw Desktop

**Нативное Windows-приложение** в стиле Telegram Desktop для общения с ИИ-агентом через [OpenClaw Gateway](https://github.com/openclaw/openclaw).

![Platform](https://img.shields.io/badge/platform-Windows_10_1809+-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![WinUI](https://img.shields.io/badge/WinUI-3-5865F2)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📸 Скриншоты

*(добавь сюда скриншот приложения)*

## ✨ Возможности

- **Telegram-подобный интерфейс**: список сессий слева, чат справа
- **Постоянные сессии**: история сохраняется локально в `%LOCALAPPDATA%/OpenClawClient/sessions/`
- **Отправка и получение файлов**: как вложение в сообщения (аналог Telegram media)
- **Inline-кнопки**: агент может отправлять кнопки быстрых действий
- **Автозагрузка медиа**: изображения, аудио, видео, документы (настраиваемый лимит, только безопасные типы)
- **Прогресс-бары**: асинхронная загрузка файлов с отображением прогресса
- **Системные уведомления**: Toast-уведомления Windows о новых сообщениях
- **Статус доставки**: галочки ⏳✓✓✓✓ как в Telegram
- **Indicate "печатает..."**: отображается пока агент генерирует ответ
- **Streaming**: ответы приходят потоком
- **Настройки**: API endpoint, токен, папка загрузок, лимит автоскачивания, уведомления

## 🛡 Безопасность

Приложение включает защиту от:
- **SSRF-атак**: редиректы отключены, DNS loopback проверка, только HTTP/HTTPS
- **Вредоносных файлов**: исполняемые файлы (exe, msi, dll, bat, ps1, sh) не скачиваются автоматически
- **XSS**: WinUI 3 TextBlock не рендерит HTML-разметку — текст сообщений полностью безопасен
- **Утечки токена**: схема URL нормализуется, лишние данные не логируются
- **Path traversal**: имена файлов проходят санитизацию всех недопустимых символов

## 📋 Системные требования

| Компонент | Минимум | Рекомендуется |
|---|---|---|
| OC | Windows 10 (1809) | Windows 11 |
| Архитектура | x64 | x64 / ARM64 |
| .NET | .NET 8 x64 SDK | .NET 8 x64 SDK |
| RAM | 256 МБ | 1 ГБ+ |
| Свободное место | 500 МБ | 2 ГБ |

## 🔧 Сборка из исходников

### 1️⃣ Установка инструментов (один раз)

#### Вариант A: Visual Studio 2022 (рекомендуется)

1. Скачай **Visual Studio 2022 Community** (бесплатно) — https://visualstudio.microsoft.com/
2. При установке выбери рабочую нагрузку:
   - **Разработка классических приложений на .NET** (.NET desktop development)
3. Убедись, что в составе выбраны:
   - .NET 8 SDK (включён по умолчанию)
   - Windows App SDK C# Templates
   - MSIX Packaging Tools

#### Вариант B: Только CLI (легковеснее)

```powershell
# 1. Установи .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# 2. Установи Windows App SDK 1.6
winget install "Windows App SDK 1.6 Runtime"

# 3. Установи MSIX Packaging SDK
winget install "MSIX Packaging SDK"
```

### 2️⃣ Клонирование

```powershell
git clone https://github.com/growbble/openclaw-desktop.git
cd openclaw-desktop
```

### 3️⃣ Сборка в Visual Studio 2022

1. Открой `OpenClawClient.sln` — **File → Open → Project/Solution**
2. Выбери конфигурацию: **Release** (или Debug для отладки)
3. Выбери платформу: **x64** (или **arm64** на ARM-устройствах)
4. **Build → Build Solution** (Ctrl+Shift+B)

Готово! `.msix`-пакет появится в:
```
OpenClawClient\bin\x64\Release\net8.0-windows10.0.19041.0\
```

### 4️⃣ Сборка через CLI

```powershell
# Release сборка для x64
dotnet build OpenClawClient.sln -c Release -p:Platform=x64

# Release сборка для ARM64
dotnet build OpenClawClient.sln -c Release -p:Platform=arm64
```

### 5️⃣ Запуск

Собранный .msix нужно установить:

```powershell
# Установка пакета
Add-AppxPackage -Path "OpenClawClient\bin\x64\Release\net8.0-windows10.0.19041.0\OpenClawClient_1.0.0.0_x64.msix"

# Если нужно установить сертификат разработчика:
# Открой .cer файл → Install Certificate → Local Machine → Trusted Root Certification Authorities
```

Или через Visual Studio: нажми **F5** (Debug) или **Ctrl+F5** (без отладки).

### ⚠️ Возможные проблемы при сборке

| Проблема | Решение |
|---|---|
| `Microsoft.WindowsAppSDK` не найден | Установи Windows App SDK 1.6 через NuGet Package Manager |
| `MSB3249` — ошибка COM-компонента | `dotnet restore` или перезапусти VS |
| `.msix` не создаётся | Проверь, что выбрана платформа x64, а не AnyCPU |
| Сертификат не подписан | VS автоматически генерирует тестовый — можно игнорировать для Dev |
| `error NETSDK1083` | Установи .NET 8 SDK |

## 🚀 Первый запуск

1. **URL сервера**: `http://ваш-vds:18789`
2. **Gateway Token**: из файла `config.yaml` Gateway, поле `gateway.auth.token`
3. **Папка загрузок**: по умолчанию `%USERPROFILE%/Downloads/OpenClaw`
4. Нажми **Проверить соединение** (зелёная точка = успех)
5. Готово — можно общаться!

## 🔌 Подключение к Gateway

Убедись, что в `config.yaml` Gateway включены нужные эндпоинты:

```yaml
gateway:
  http:
    endpoints:
      chatCompletions:
        enabled: true
      responses:
        enabled: true    # для отправки файлов
      models:
        enabled: true    # для проверки соединения
```

## 📁 Структура проекта

```
OpenClawClient/
├── App.xaml / App.xaml.cs           — точка входа, DI
├── Package.appxmanifest             — манифест пакета
│
├── Models/
│   ├── AppConfig.cs                 — настройки (URL, токен, папки)
│   ├── ChatSession.cs               — сессия (аналог чата в Telegram)
│   ├── ChatMessage.cs               — сообщение с вложениями
│   ├── Attachment.cs                — файловое вложение
│   ├── InlineButton.cs              — inline-кнопка от агента
│   ├── AgentResponse.cs             — ответ агента
│   ├── OpenAIModels.cs              — DTO для /v1/chat/completions
│   └── ReceivedFile.cs              — скачанный файл
│
├── Services/
│   ├── ConfigService.cs             — управление настройками (JSON)
│   ├── SessionService.cs            — сериализация сессий
│   ├── OpenClawGatewayService.cs    — HTTP-клиент Gateway
│   ├── FileDownloadService.cs       — автоскачивание файлов
│   ├── PollingService.cs            — фоновый опрос новых сообщений
│   └── NotificationService.cs       — Toast-уведомления Windows
│
├── ViewModels/
│   └── MainViewModel.cs             — бизнес-логика чата
│
├── Views/
│   ├── MainWindow.xaml / .cs        — главное окно
│   ├── MainChatPage.xaml / .cs      — страница чата
│   └── SettingsPage.xaml / .cs      — настройки
│
├── Converters/
│   └── BoolConverters.cs            — XAML-конвертеры
│
└── Assets/
    └── icon.ico                     — иконка приложения
```

## ⚙️ API эндпоинты Gateway

| Метод | Эндпоинт | Назначение |
|---|---|---|
| POST | `/v1/chat/completions` | Отправка сообщений (OpenAI-формат) |
| POST | `/v1/responses` | Отправка файлов (OpenResponses) |
| GET | `/v1/models` | Проверка соединения |

Аутентификация: `Authorization: Bearer <token>`

## 📄 Лицензия

MIT — делай что хочешь, но без гарантий.
