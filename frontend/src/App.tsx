import { AnimatePresence } from "framer-motion";
import { lazy, Suspense } from "react";
import { Route, Routes, useLocation } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { LoadingState } from "./components/feedback";
import { PageMotion } from "./components/motion";

const DashboardPage = lazy(() => import("./pages/DashboardPage").then((module) => ({ default: module.DashboardPage })));
const AnalyticsPage = lazy(() => import("./pages/AnalyticsPage").then((module) => ({ default: module.AnalyticsPage })));
const AiPage = lazy(() => import("./pages/AiPage").then((module) => ({ default: module.AiPage })));
const CategoriesPage = lazy(() => import("./pages/CategoriesPage").then((module) => ({ default: module.CategoriesPage })));
const ProductsPage = lazy(() => import("./pages/ProductsPage").then((module) => ({ default: module.ProductsPage })));
const CustomersPage = lazy(() => import("./pages/CustomersPage").then((module) => ({ default: module.CustomersPage })));
const OrdersPage = lazy(() => import("./pages/OrdersPage").then((module) => ({ default: module.OrdersPage })));
const NewOrderPage = lazy(() => import("./pages/NewOrderPage").then((module) => ({ default: module.NewOrderPage })));
const OrderDetailPage = lazy(() => import("./pages/OrderDetailPage").then((module) => ({ default: module.OrderDetailPage })));
const NotFoundPage = lazy(() => import("./pages/NotFoundPage").then((module) => ({ default: module.NotFoundPage })));

export default function App() {
  const location = useLocation();
  return <AppShell><Suspense fallback={<LoadingState label="Ekran hazırlanıyor" />}><AnimatePresence mode="wait" initial={false}><PageMotion key={location.pathname}><Routes location={location}><Route path="/" element={<DashboardPage />} /><Route path="/analytics" element={<AnalyticsPage />} /><Route path="/ai" element={<AiPage />} /><Route path="/categories" element={<CategoriesPage />} /><Route path="/products" element={<ProductsPage />} /><Route path="/customers" element={<CustomersPage />} /><Route path="/orders" element={<OrdersPage />} /><Route path="/orders/new" element={<NewOrderPage />} /><Route path="/orders/:id" element={<OrderDetailPage />} /><Route path="*" element={<NotFoundPage />} /></Routes></PageMotion></AnimatePresence></Suspense></AppShell>;
}
