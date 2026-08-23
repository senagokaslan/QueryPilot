export type OrderStatus = "Pending" | "Completed" | "Cancelled";
export type AnalyticsGranularity = "Daily" | "Weekly" | "Monthly";
export type TopProductsMetric = "Quantity" | "Revenue";
export type ComparisonStatus = "Calculated" | "NoPreviousData" | "NoCurrentOrPreviousData";
export type ReturnRateStatus = "Calculated" | "NoSalesBaseline";
export type AiAnalyticsExecutionStatus = "Completed" | "NeedsClarification" | "Unsupported";
export type AiExplanationStatus = "Available" | "Unavailable" | "RejectedUnsafe";

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export interface Category {
  id: number;
  name: string;
  isActive: boolean;
  createdAt: string;
}

export interface ProductCategorySummary {
  id: number;
  name: string;
  isActive: boolean;
}

export interface Product {
  id: number;
  name: string;
  sku: string;
  category: ProductCategorySummary;
  unitPrice: number;
  isActive: boolean;
  createdAt: string;
}

export interface Customer {
  id: number;
  name: string;
  email: string;
  city: string;
  isActive: boolean;
  createdAt: string;
}

export interface OrderListItem {
  id: number;
  customerId: number;
  orderDate: string;
  status: OrderStatus;
  totalAmount: number;
  itemCount: number;
}

export interface OrderItemReturnSummary {
  returnCount: number;
  returnedQuantity: number;
  returnedAmount: number;
}

export interface OrderDetail {
  id: number;
  customer: { id: number; name: string; email: string; city: string };
  orderDate: string;
  status: OrderStatus;
  totalAmount: number;
  items: Array<{
    id: number;
    product: { id: number; name: string; sku: string };
    quantity: number;
    unitPrice: number;
    lineTotal: number;
    returnSummary: OrderItemReturnSummary | null;
  }>;
}

export interface ReturnRecord {
  id: number;
  orderItemId: number;
  quantity: number;
  reason: string;
  returnDate: string;
  amount: number;
  remainingReturnableQuantity: number;
}

export interface ComparisonPeriod {
  currentFromUtc: string;
  currentToUtc: string;
  previousFromUtc: string;
  previousToUtc: string;
  durationTicks: number;
  policy: string;
}

export interface MetricComparison {
  currentValue: number;
  previousValue: number;
  percentageChange: number | null;
  status: ComparisonStatus;
}

export interface SalesSummary {
  fromUtc: string;
  toUtc: string;
  totalRevenue: number;
  orderCount: number;
  unitsSold: number;
  averageOrderValue: number;
  comparisonPeriod: ComparisonPeriod;
  revenueComparison: MetricComparison;
}

export interface AnalyticsChartPoint {
  periodStart: string;
  label: string;
  value: number;
}

export interface SalesTrend {
  fromUtc: string;
  toUtc: string;
  granularity: AnalyticsGranularity;
  revenue: AnalyticsChartPoint[];
  orderCount: AnalyticsChartPoint[];
  unitsSold: AnalyticsChartPoint[];
}

export interface TopProduct {
  rank: number;
  productId: number;
  name: string;
  sku: string;
  quantity: number;
  revenue: number;
}

export interface TopProducts {
  fromUtc: string;
  toUtc: string;
  metric: TopProductsMetric;
  limit: number;
  categoryId: number | null;
  items: TopProduct[];
}

export interface CategoryPerformanceItem {
  rank: number;
  categoryId: number;
  name: string;
  revenue: number;
  unitsSold: number;
  revenueSharePercentage: number;
  revenueComparison: MetricComparison;
}

export interface CategoryPerformance {
  fromUtc: string;
  toUtc: string;
  totalRevenue: number;
  items: CategoryPerformanceItem[];
  comparisonPeriod: ComparisonPeriod;
  totalRevenueComparison: MetricComparison;
}

export interface ReturnAnalytics {
  fromUtc: string;
  toUtc: string;
  returnRecordCount: number;
  returnedQuantity: number;
  totalReturnAmount: number;
  soldQuantity: number;
  returnRatePercentage: number | null;
  returnRateStatus: ReturnRateStatus;
  reasons: Array<{ reason: string; returnCount: number; quantity: number; amount: number }>;
  mostReturnedProducts: Array<{
    rank: number;
    productId: number;
    name: string;
    sku: string;
    returnCount: number;
    quantity: number;
    amount: number;
  }>;
  metadata: {
    returnRateDefinition: string;
    returnDateFilter: string;
    salesDateFilter: string;
    mostReturnedProductsLimit: number;
  };
}

export interface AiQuestionUnderstanding {
  analysis: string;
  period: string | null;
  from: string | null;
  to: string | null;
  productName: string | null;
  categoryName: string | null;
  granularity: string | null;
  metric: string | null;
  limit: number | null;
}

export interface ValidatedAnalyticsIntent {
  analysis: string;
  fromUtc: string;
  toUtc: string;
  productId: number | null;
  productName: string | null;
  categoryId: number | null;
  categoryName: string | null;
  granularity: AnalyticsGranularity | null;
  metric: TopProductsMetric | null;
  limit: number | null;
}

export interface AiExecutionResult {
  status: AiAnalyticsExecutionStatus;
  message: string;
  intent: ValidatedAnalyticsIntent | null;
  data: SalesSummary | SalesTrend | TopProducts | CategoryPerformance | ReturnAnalytics | null;
  chartData: { revenue: AnalyticsChartPoint[]; orderCount: AnalyticsChartPoint[]; unitsSold: AnalyticsChartPoint[] } | null;
  warning: string | null;
  explanation: { status: AiExplanationStatus; text: string | null } | null;
  clarification: {
    originalQuestion: string;
    currentIntent: AiQuestionUnderstanding;
    requiredFields: Array<{ field: string; question: string; allowedValues: string[] }>;
    retryInstruction: string;
  } | null;
  unsupported: { supportedAnalyses: string[]; exampleQuestions: string[] } | null;
}

export interface HealthResponse {
  status: string;
  checks: Array<{ name: string; status: string; description: string; durationMs: number }>;
  totalDurationMs: number;
}

export interface PageQuery {
  page?: number;
  pageSize?: number;
}
