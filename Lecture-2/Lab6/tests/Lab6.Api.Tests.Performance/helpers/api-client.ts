/**
 * HTTP-клієнт для Orders API.
 *
 * Обгортки над k6/http для CRUD-операцій із замовленнями.
 * Використовує ендпоінти та заголовки з config.ts.
 */

import http, { RefinedResponse, ResponseType } from "k6/http";
import {
  ENDPOINTS,
  HEADERS,
  CreateOrderRequest,
  UpdateOrderStatusRequest,
} from "./config.ts";

/** GET /api/orders — отримати список усіх замовлень */
export function getOrders(): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.orders, { headers: HEADERS });
}

/** GET /api/orders/{id} — отримати замовлення за ідентифікатором */
export function getOrderById(id: number): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.orderById(id), { headers: HEADERS });
}

/** POST /api/orders — створити нове замовлення */
export function createOrder(
  payload: CreateOrderRequest,
): RefinedResponse<ResponseType> {
  return http.post(ENDPOINTS.orders, JSON.stringify(payload), {
    headers: HEADERS,
  });
}

/** PUT /api/orders/{id}/status — оновити статус замовлення */
export function updateOrderStatus(
  id: number,
  payload: UpdateOrderStatusRequest,
): RefinedResponse<ResponseType> {
  return http.put(ENDPOINTS.orderStatus(id), JSON.stringify(payload), {
    headers: HEADERS,
  });
}

/** DELETE /api/orders/{id} — видалити замовлення */
export function deleteOrder(id: number): RefinedResponse<ResponseType> {
  return http.del(ENDPOINTS.orderById(id), undefined, { headers: HEADERS });
}
