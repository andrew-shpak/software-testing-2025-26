/**
 * Stress тест — пошук точки відмови API.
 *
 * Мета: визначити максимальну пропускну здатність та поведінку
 * системи при поступовому збільшенні навантаження понад норму.
 * Профіль навантаження:
 *   1 хв  — розгін до 10 VUs (нижче норми)
 *   2 хв  — утримання 10 VUs
 *   1 хв  — зростання до 50 VUs (біля точки відмови)
 *   2 хв  — утримання 50 VUs
 *   1 хв  — зростання до 100 VUs (перевищення точки відмови)
 *   2 хв  — утримання 100 VUs
 *   2 хв  — відновлення до 0
 *
 * Пороги послаблені: p(95)<1000ms, p(99)<2000ms.
 *
 * Запуск: k6 run scripts/stress-test.ts
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
    { duration: "1m", target: 10 },   // нижче нормального навантаження
    { duration: "2m", target: 10 },   // утримання
    { duration: "1m", target: 50 },   // наближення до точки відмови
    { duration: "2m", target: 50 },   // утримання
    { duration: "1m", target: 100 },  // перевищення точки відмови
    { duration: "2m", target: 100 },  // утримання
    { duration: "2m", target: 0 },    // відновлення
  ],
  thresholds: {
    ...THRESHOLDS,
    // Послаблені пороги для стрес-сценарію
    http_req_duration: ["p(95)<1000", "p(99)<2000"],
  },
};

export default function () {
  // Читання списку замовлень
  const allOrders = getOrders();
  check(allOrders, {
    "GET /api/orders status is 200": (r) => r.status === 200,
  });

  // Створення замовлення під навантаженням
  const created = createOrder({
    customerName: `Stress User ${__VU}`,
    items: [{ productName: "Stress Product", quantity: 1, price: 5.0 }],
  });
  check(created, {
    "POST /api/orders status is 201": (r) => r.status === 201,
  });

  // Отримання та очистка створеного замовлення
  if (created.status === 201) {
    const order = parseBody<OrderResponse>(created);
    getOrderById(order.id);
    deleteOrder(order.id);
  }

  sleep(0.5);
}
