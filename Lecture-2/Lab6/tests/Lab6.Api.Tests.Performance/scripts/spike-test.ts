/**
 * Spike тест — перевірка реакції API на раптовий сплеск навантаження.
 *
 * Мета: оцінити, як система обробляє різкий стрибок трафіку
 * та наскільки швидко відновлюється після нього.
 * Профіль навантаження:
 *   30с  — прогрів на 5 VUs
 *   10с  — миттєвий сплеск до 200 VUs
 *   1 хв — утримання 200 VUs
 *   10с  — різке падіння до 5 VUs
 *   1 хв — відновлення на 5 VUs
 *   30с  — плавне завершення
 *
 * Пороги послаблені: p(95)<2000ms, помилки <5%.
 *
 * Запуск: k6 run scripts/spike-test.ts
 */

import { check, sleep } from "k6";
import { Options } from "k6/options";
import { THRESHOLDS, OrderResponse, parseBody } from "../helpers/config.ts";
import {
  createOrder,
  getOrders,
  deleteOrder,
} from "../helpers/api-client.ts";

export const options: Options = {
  stages: [
    { duration: "30s", target: 5 },    // прогрів
    { duration: "10s", target: 200 },   // сплеск навантаження
    { duration: "1m", target: 200 },    // утримання піку
    { duration: "10s", target: 5 },     // різке падіння
    { duration: "1m", target: 5 },      // фаза відновлення
    { duration: "30s", target: 0 },     // завершення
  ],
  thresholds: {
    ...THRESHOLDS,
    // Послаблені пороги для spike-сценарію
    http_req_duration: ["p(95)<2000"],
    http_req_failed: ["rate<0.05"],
  },
};

export default function () {
  // Читання замовлень під сплеском
  const allOrders = getOrders();
  check(allOrders, {
    "GET /api/orders status is 200": (r) => r.status === 200,
  });

  // Створення замовлення під сплеском
  const created = createOrder({
    customerName: `Spike User ${__VU}`,
    items: [{ productName: "Spike Product", quantity: 1, price: 10.0 }],
  });
  check(created, {
    "POST /api/orders succeeded": (r) => r.status === 201,
  });

  // Очистка створених даних
  if (created.status === 201) {
    const order = parseBody<OrderResponse>(created);
    deleteOrder(order.id);
  }

  sleep(0.3);
}
