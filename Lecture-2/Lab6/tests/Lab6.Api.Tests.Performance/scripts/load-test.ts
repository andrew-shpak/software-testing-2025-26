/**
 * Load тест — перевірка поведінки API під стандартним навантаженням.
 *
 * Мета: оцінити продуктивність при очікуваній кількості користувачів.
 * Профіль навантаження:
 *   1 хв  — розгін до 10 VUs
 *   3 хв  — стабільне навантаження 10 VUs
 *   1 хв  — плавне зниження до 0
 *
 * Запуск: k6 run scripts/load-test.ts
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
  stages: [
    { duration: "1m", target: 10 },  // розгін до 10 VUs
    { duration: "3m", target: 10 },  // стабільне навантаження
    { duration: "1m", target: 0 },   // плавне зниження
  ],
  thresholds: THRESHOLDS,
};

export default function () {
  // Отримати всі замовлення
  const allOrders = getOrders();
  check(allOrders, {
    "GET /api/orders returns 200": (r) => r.status === 200,
  });

  // Створити замовлення з унікальним ім'ям (VU + ітерація)
  const created = createOrder({
    customerName: `Load Test User ${__VU}-${__ITER}`,
    items: [
      { productName: "Product A", quantity: 2, price: 19.99 },
      { productName: "Product B", quantity: 1, price: 49.99 },
    ],
  });
  check(created, {
    "POST /api/orders returns 201": (r) => r.status === 201,
  });

  // Перевірити отримання за ID та очистити дані
  if (created.status === 201) {
    const order = parseBody<OrderResponse>(created);

    const fetched = getOrderById(order.id);
    check(fetched, {
      "GET /api/orders/{id} returns 200": (r) => r.status === 200,
    });

    deleteOrder(order.id);
  }

  sleep(1);
}
