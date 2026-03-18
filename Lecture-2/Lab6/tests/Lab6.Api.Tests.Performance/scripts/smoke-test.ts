/**
 * Smoke тест — швидка перевірка працездатності API.
 *
 * Мета: переконатися, що API запущено та основні ендпоінти
 * відповідають коректно. Виконується з мінімальним навантаженням
 * (1 віртуальний користувач, 30 секунд).
 *
 * Запуск: k6 run scripts/smoke-test.ts
 */

import { check, sleep } from "k6";
import { Options } from "k6/options";
import { THRESHOLDS, OrderResponse, parseBody } from "../helpers/config.ts";
import {
  createOrder,
  getOrders,
  getOrderById,
  deleteOrder,
} from "../helpers/api-client.ts";

export const options: Options = {
  vus: 1,        // 1 віртуальний користувач
  duration: "30s", // тривалість тесту
  thresholds: THRESHOLDS,
};

export default function () {
  // Крок 1: Отримати список усіх замовлень
  const allOrders = getOrders();
  check(allOrders, {
    "GET /api/orders returns 200": (r) => r.status === 200,
  });

  // Крок 2: Створити нове замовлення
  const created = createOrder({
    customerName: "k6 Smoke Test",
    items: [{ productName: "Test Product", quantity: 1, price: 9.99 }],
  });
  check(created, {
    "POST /api/orders returns 201": (r) => r.status === 201,
  });

  // Крок 3: Перевірити отримання та видалення створеного замовлення
  if (created.status === 201) {
    const order = parseBody<OrderResponse>(created);

    const fetched = getOrderById(order.id);
    check(fetched, {
      "GET /api/orders/{id} returns 200": (r) => r.status === 200,
    });

    const deleted = deleteOrder(order.id);
    check(deleted, {
      "DELETE /api/orders/{id} returns 204": (r) => r.status === 204,
    });
  }

  sleep(1);
}
