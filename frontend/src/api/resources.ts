import { apiRequest, toQuery } from "./client";
import type {
  AiExecutionResult,
  Category,
  Customer,
  HealthResponse,
  OrderDetail,
  OrderListItem,
  OrderStatus,
  PagedResponse,
  Product,
  ReturnRecord,
} from "../types/api";

export interface ListQuery { page?: number; pageSize?: number; isActive?: boolean }

export const categoriesApi = {
  list: (query: ListQuery = {}) => apiRequest<PagedResponse<Category>>(`/api/categories${toQuery(query)}`),
  get: (id: number) => apiRequest<Category>(`/api/categories/${id}`),
  create: (body: { name: string }) => apiRequest<Category>("/api/categories", { method: "POST", body: JSON.stringify(body) }),
  update: (id: number, body: { name: string; isActive: boolean }) => apiRequest<Category>(`/api/categories/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deactivate: (id: number) => apiRequest<void>(`/api/categories/${id}`, { method: "DELETE" }),
};

export interface ProductListQuery extends ListQuery { categoryId?: number; search?: string }
export interface ProductInput { name: string; sku: string; categoryId: number; unitPrice: number; isActive: boolean }
export const productsApi = {
  list: (query: ProductListQuery = {}) => apiRequest<PagedResponse<Product>>(`/api/products${toQuery(query)}`),
  get: (id: number) => apiRequest<Product>(`/api/products/${id}`),
  create: (body: ProductInput) => apiRequest<Product>("/api/products", { method: "POST", body: JSON.stringify(body) }),
  update: (id: number, body: ProductInput) => apiRequest<Product>(`/api/products/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deactivate: (id: number) => apiRequest<void>(`/api/products/${id}`, { method: "DELETE" }),
};

export interface CustomerListQuery extends ListQuery { search?: string; city?: string }
export interface CustomerInput { name: string; email: string; city: string; isActive: boolean }
export const customersApi = {
  list: (query: CustomerListQuery = {}) => apiRequest<PagedResponse<Customer>>(`/api/customers${toQuery(query)}`),
  get: (id: number) => apiRequest<Customer>(`/api/customers/${id}`),
  create: (body: CustomerInput) => apiRequest<Customer>("/api/customers", { method: "POST", body: JSON.stringify(body) }),
  update: (id: number, body: CustomerInput) => apiRequest<Customer>(`/api/customers/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deactivate: (id: number) => apiRequest<void>(`/api/customers/${id}`, { method: "DELETE" }),
};

export interface OrderListQuery { page?: number; pageSize?: number; from?: string; to?: string; status?: OrderStatus; customerId?: number }
export const ordersApi = {
  list: (query: OrderListQuery = {}) => apiRequest<PagedResponse<OrderListItem>>(`/api/orders${toQuery(query)}`),
  get: (id: number) => apiRequest<OrderDetail>(`/api/orders/${id}`),
  create: (body: { customerId: number; items: Array<{ productId: number; quantity: number }> }) => apiRequest<OrderDetail>("/api/orders", { method: "POST", body: JSON.stringify(body) }),
};

export const returnsApi = {
  create: (body: { orderItemId: number; quantity: number; reason: string }) => apiRequest<ReturnRecord>("/api/returns", { method: "POST", body: JSON.stringify(body) }),
};

export const aiApi = {
  query: (question: string) => apiRequest<AiExecutionResult>("/api/ai/query", { method: "POST", body: JSON.stringify({ question }) }),
};

export const healthApi = {
  get: () => apiRequest<HealthResponse>("/api/health"),
};
