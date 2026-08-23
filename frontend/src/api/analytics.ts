import { apiRequest, toQuery } from "./client";
import type {
  AnalyticsGranularity,
  CategoryPerformance,
  ReturnAnalytics,
  SalesSummary,
  SalesTrend,
  TopProducts,
  TopProductsMetric,
} from "../types/api";

export interface AnalyticsRange { from: string; to: string }

export const analyticsApi = {
  summary: (range: AnalyticsRange) => apiRequest<SalesSummary>(`/api/analytics/summary${toQuery(range)}`),
  trend: (range: AnalyticsRange, granularity: AnalyticsGranularity) =>
    apiRequest<SalesTrend>(`/api/analytics/sales-trend${toQuery({ ...range, granularity })}`),
  topProducts: (range: AnalyticsRange, metric: TopProductsMetric, limit = 5, categoryId?: number) =>
    apiRequest<TopProducts>(`/api/analytics/top-products${toQuery({ ...range, metric, limit, categoryId })}`),
  categories: (range: AnalyticsRange) =>
    apiRequest<CategoryPerformance>(`/api/analytics/categories${toQuery(range)}`),
  returns: (range: AnalyticsRange) => apiRequest<ReturnAnalytics>(`/api/analytics/returns${toQuery(range)}`),
};
