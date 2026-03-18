/**
 * Конфігурація k6 тестів продуктивності для Orders API.
 *
 * Містить базову URL, маршрути ендпоінтів, HTTP-заголовки,
 * пороги якості та TypeScript-інтерфейси моделей API.
 *
 * Щоб змінити базову URL, передайте змінну оточення:
 *   k6 run -e BASE_URL=http://localhost:8080 scripts/smoke-test.ts
 */

import { RefinedResponse, ResponseType } from "k6/http";

// Базова URL API — можна перевизначити через змінну оточення BASE_URL
export const BASE_URL: string = __ENV.BASE_URL || "http://localhost:5000";

// Маршрути ендпоінтів Orders API
export const ENDPOINTS = {
  orders: `${BASE_URL}/api/orders`,
  orderById: (id: number) => `${BASE_URL}/api/orders/${id}`,
  orderStatus: (id: number) => `${BASE_URL}/api/orders/${id}/status`,
} as const;

// Стандартні заголовки для JSON-запитів
export const HEADERS: Record<string, string> = {
  "Content-Type": "application/json",
};

// Пороги якості за замовчуванням:
// - 95-й перцентиль відповіді < 500ms
// - 99-й перцентиль відповіді < 1000ms
// - Частка помилок < 1%
export const THRESHOLDS: Record<string, string[]> = {
  http_req_duration: ["p(95)<500", "p(99)<1000"],
  http_req_failed: ["rate<0.01"],
};

// ——— Інтерфейси моделей API ———

/** Елемент замовлення */
export interface OrderItem {
  productName: string;
  quantity: number;
  price: number;
}

/** Тіло запиту на створення замовлення (POST /api/orders) */
export interface CreateOrderRequest {
  customerName: string;
  items: OrderItem[];
}

/** Тіло запиту на оновлення статусу замовлення (PUT /api/orders/{id}/status) */
export interface UpdateOrderStatusRequest {
  status: string;
}

/** Відповідь API для замовлення */
export interface OrderResponse {
  id: number;
  customerName: string;
  status: string;
  items: OrderItem[];
  totalPrice: number;
  createdAt: string;
}

/** Типізований парсинг JSON-тіла відповіді */
export function parseBody<T>(res: RefinedResponse<ResponseType>): T {
  return JSON.parse(res.body as string) as T;
}
