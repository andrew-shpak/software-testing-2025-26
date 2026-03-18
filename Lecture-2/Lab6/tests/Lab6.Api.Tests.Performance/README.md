# Lab6.Api.Tests.Performance

Тести продуктивності (performance tests) для Lab6 Orders API з використанням [k6](https://k6.io/) v1.0+ та **TypeScript**.

## Передумови

- [k6 v1.0+](https://grafana.com/docs/k6/latest/set-up/install-k6/) встановлено (нативна підтримка TypeScript)
- API запущено та доступне (за замовчуванням `http://localhost:5000`)

## Типи тестів

| Скрипт | Опис | VUs | Тривалість |
|--------|------|-----|------------|
| `smoke-test.ts` | Базова перевірка працездатності | 1 | 30s |
| `load-test.ts` | Стандартне навантаження | 10 | 5m |
| `stress-test.ts` | Пошук точки відмови | 10→100 | ~11m |
| `spike-test.ts` | Раптовий сплеск навантаження | 5→200→5 | ~3.5m |

## Структура проєкту

```
Lab6.Api.Tests.Performance/
├── package.json          # npm-скрипти для запуску
├── helpers/
│   ├── config.ts         # BASE_URL, ендпоінти, пороги, типи
│   └── api-client.ts     # Типізовані HTTP-хелпери для Orders API
└── scripts/
    ├── smoke-test.ts     # Smoke тест
    ├── load-test.ts      # Load тест
    ├── stress-test.ts    # Stress тест
    └── spike-test.ts     # Spike тест
```

## Запуск

```bash
# Smoke тест (швидка перевірка)
k6 run scripts/smoke-test.ts

# Load тест (стандартне навантаження)
k6 run scripts/load-test.ts

# Stress тест (пошук межі)
k6 run scripts/stress-test.ts

# Spike тест (раптовий сплеск)
k6 run scripts/spike-test.ts
```

### Зміна базової URL

```bash
k6 run -e BASE_URL=http://localhost:8080 scripts/load-test.ts
```

### Збереження результатів у JSON

```bash
k6 run --out json=results.json scripts/load-test.ts
```

## Пороги (Thresholds)

Значення за замовчуванням (визначені у `helpers/config.ts`):

| Метрика | Поріг |
|---------|-------|
| `http_req_duration` p(95) | < 500ms |
| `http_req_duration` p(99) | < 1000ms |
| `http_req_failed` | < 1% |

Stress та Spike тести мають послаблені пороги для врахування екстремального навантаження.

## Ендпоінти, що тестуються

- `GET /api/orders` — отримання всіх замовлень
- `GET /api/orders/{id}` — отримання замовлення за ID
- `POST /api/orders` — створення замовлення
- `PUT /api/orders/{id}/status` — оновлення статусу
- `DELETE /api/orders/{id}` — видалення замовлення
