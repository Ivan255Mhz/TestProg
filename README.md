# Тестовое задание

## Стек

- .NET 8.0 (C#)
- gRPC (Grpc.Net.Client, proto-файл из задания)
- PostgreSQL + EF Core (Npgsql)
- WPF 
- Тесты: xUnit

## Архитектура

```
src/
  Sms.Contracts   — модели («Блюдо», «Заказ») и транспортный формат 
  Sms.Client      — библиотека (.dll): интерфейс ISmsClient, HTTP-клиент, gRPC-клиент, фабрика
  Sms.ConsoleApp  — консольное приложение: меню → PostgreSQL → ввод заказа → отправка на сервер
  Sms.WpfApp      — WPF-приложение: таблица «Поле / Значение / Комментарий» для переменных среды
tests/
  Sms.Client.Tests — unit-тесты HTTP-клиента
```

```
ConsoleApp → ISmsClient → HttpSmsClient  (Basic auth, JSON)
                        → GrpcSmsClient  (gRPC, без авторизации)
```

Транспорт (Http / Grpc) переключается в конфиге

## Как запустить

### 1. Установить .NET 8 SDK

Скачайте и установите: https://dotnet.microsoft.com/download/dotnet/8.0

Проверка:

```
dotnet --version
```

### 2. Установить Docker Desktop 

Скачайте и установите: https://www.docker.com/products/docker-desktop/
Запустите Docker Desktop и дождитесь статуса «Running».

Проверка:

```
docker --version
```

### 3. Поднять базу данных PostgreSQL

В корне репозитория:

```
docker compose up -d
```

Проверка:

```
docker ps
```

В списке должен быть контейнер `sms-postgres`.

### 4. Запустить консольное приложение

```
dotnet run --project src/Sms.ConsoleApp
```

Программа создаст базу и таблицу, получит меню, сохранит его в PostgreSQL и выведет в консоль.

Ввод заказа одной строкой:

```
P001:2;G001:0.5
```

Валидация: код должен существовать в меню, количество больше нуля. Выход: `exit`.

### 5. Запустить WPF-приложение

```
dotnet run --project src/Sms.WpfApp
```

Откроется таблица «Поле / Значение / Комментарий». Изменённые значения сохраняются на уровне пользователя Windows и доступны другим приложениям.

## Конфигурация

### src/Sms.ConsoleApp/appsettings.json

```json
{
  "ConnectionStrings": {
    "SmsDb": "Host=localhost;Port=5433;Database=sms;Username=sms;Password=sms_password"
  },
  "SmsClient": {
    "TransportType": "Http",
    "BaseUrl": "http://localhost:5010",
    "Endpoint": "/api/sms",
    "Username": "test_user",
    "Password": "test_password",
    "TimeoutSeconds": 30
  }
}
```

- `SmsDb` — строка подключения к PostgreSQL (должна совпадать с docker-compose.yaml)
- `TransportType` — `Http` или `Grpc`
- `BaseUrl` — адрес сервера (`http://localhost:5010` для HTTP, `http://localhost:5011` для gRPC)
- `Endpoint` — общий endpoint сервера
- `Username` / `Password` — учётные данные Basic-аутентификации

Лог: `src/Sms.ConsoleApp/bin/Debug/net8.0/logs/test-sms-console-app-yyyyMMdd.log`

### src/Sms.WpfApp/appsettings.json

```json
{
  "EnvVariables": {
    "Names": [
      "SMS_TEST_VAR1",
      "SMS_TEST_VAR2"
    ],
    "DefaultValue": ""
  }
}
```

- `Names` — массив имён переменных среды для таблицы
- `DefaultValue` — значение, которое присваивается переменной, если она не существует

Лог: `src/Sms.WpfApp/bin/Debug/net8.0-windows/logs/test-sms-wpf-app-yyyyMMdd.log`

## Тесты

```
dotnet test
```
