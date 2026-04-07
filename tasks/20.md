# Завдання 20: Оголошення нерухомості

## Домен

Платформа оголошень нерухомості з об'єктами, агентами та запитами.

## Сутності

- **Property**: Id, Title, Description, Address, City, Price, Area (кв.м), Bedrooms, Bathrooms, Type (Apartment/House/Commercial), Status (Available/Sold/Rented), AgentId, ListedAt
- **Agent**: Id, FirstName, LastName, Email, Phone, LicenseNumber
- **Inquiry**: Id, PropertyId, Name, Email, Phone, Message, CreatedAt, IsResponded

## Ендпоінти

| Метод | Маршрут | Опис |
|--------|-------|-------------|
| GET | /api/properties | Отримати об'єкти (фільтр за містом, типом, діапазоном цін, кількістю спалень) |
| POST | /api/properties | Розмістити об'єкт |
| GET | /api/properties/{id} | Отримати деталі об'єкта |
| PUT | /api/properties/{id} | Оновити об'єкт |
| PATCH | /api/properties/{id}/status | Змінити статус об'єкта |
| POST | /api/properties/{id}/inquiries | Подати запит |
| GET | /api/agents/{id}/properties | Отримати об'єкти за агентом |
| GET | /api/agents/{id}/inquiries | Отримати запити щодо об'єктів агента |

## Бізнес-правила

- Price та Area мають бути додатними
- LicenseNumber має бути унікальним
- Не можна подати запит щодо проданих/зданих в оренду об'єктів
- Агент має існувати при розміщенні об'єкта
- Bedrooms та Bathrooms мають бути невід'ємними цілими числами

## Обсяг тестування

- **Юніт-тести**: Валідація ціни/площі, правила переходу статусу, перевірка можливості запиту
- **Інтеграційні тести (WebApplicationFactory)**: Процес розміщення, пошук з кількома фільтрами, подання запиту
- **Тести бази даних (Testcontainers)**: Складні запити з фільтрами, зв'язок агент-об'єкт, відстеження запитів
- **Тести продуктивності (k6)**: Навантажувальне тестування пошуку об'єктів з кількома фільтрами, стрес-тестування одночасних запитів

## Поля AutoFixture

Поля для автогенерації за допомогою AutoFixture (не критичні для бізнес-логіки, потрібні лише валідні дані):

- **Property**: Title, Description, Address, City, ListedAt
- **Agent**: FirstName, LastName, Email, Phone
- **Inquiry**: Name, Phone, Message, CreatedAt

> Поля Price, Area, Bedrooms, Bathrooms, Type, Status, AgentId, LicenseNumber, Email, IsResponded мають бізнес-правила і повинні встановлюватися явно в тестах.

## База даних

Використовуйте **PostgreSQL** як СУБД. Для Testcontainers використовуйте пакет `Testcontainers.PostgreSql`.

## Наповнення бази даних

Для тестів продуктивності та інтеграційних тестів база даних повинна бути попередньо наповнена **щонайменше 10 000 записами**, розподіленими між усіма сутностями. Використовуйте AutoFixture (або Bogus) для генерації реалістичних тестових даних. Розподіл має відображати реальні співвідношення між батьківськими та дочірніми сутностями.

## GitHub

- Проєкт має бути розміщений у **публічному GitHub-репозиторії**
- Кожне завдання повинно бути оформлене в окремому **Pull Request**
- Репозиторій повинен містити налаштований **CI pipeline** (GitHub Actions), який автоматично запускає тести при кожному push/pull request
