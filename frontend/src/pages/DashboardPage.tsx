import { keepPreviousData, useQueries } from "@tanstack/react-query";
import { motion, useReducedMotion } from "framer-motion";
import { ArrowDownRight, ArrowUpRight } from "lucide-react";
import { useMemo, useState } from "react";
import { Area, AreaChart, CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { analyticsApi } from "../api/analytics";
import { errorMessage } from "../api/client";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback";
import { AnimatedNumber, easeOutExpo } from "../components/motion";
import { ChartTooltip } from "../components/ui";
import { currency, dateInputValue, decimal, integer, toUtcExclusiveEnd, toUtcStart } from "../lib/format";
import { chartColors, chartTheme } from "../lib/theme";

function initialRange() { const to = new Date(); const from = new Date(); from.setMonth(from.getMonth() - 3); return { from: dateInputValue(from), to: dateInputValue(to) }; }

function KpiCard({ label, value, format, detail, tone, data, delay }: { label: string; value: number | null; format: (value: number) => string; detail: string; tone: "primary" | "success" | "violet" | "warning"; data: number[]; delay: number }) {
  const reduced = useReducedMotion();
  const chartData = data.map((point, index) => ({ index, value: point }));
  return <motion.article className="panel kpi-card" initial={reduced ? false : { opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: reduced ? 0 : delay, duration: .48, ease: easeOutExpo }} whileHover={reduced ? undefined : { y: -4 }}><div className="kpi-top"><p className="eyebrow">{label}</p>{chartData.length > 1 && <div className="sparkline"><ResponsiveContainer width="100%" height="100%"><LineChart data={chartData}><Line type="monotone" dataKey="value" stroke={`var(--${tone})`} strokeWidth={2} dot={false} isAnimationActive={!reduced} animationDuration={650} /></LineChart></ResponsiveContainer></div>}</div><p className="kpi-value">{value == null ? "Veri yok" : <AnimatedNumber value={value} format={format} />}</p><p className="kpi-detail">{detail}</p></motion.article>;
}

export function DashboardPage() {
  const [draft, setDraft] = useState(initialRange); const [range, setRange] = useState(initialRange); const reduced = useReducedMotion();
  const utcRange = useMemo(() => ({ from: toUtcStart(range.from), to: toUtcExclusiveEnd(range.to) }), [range]);
  const queries = useQueries({ queries: [
    { queryKey: ["analytics", "summary", utcRange], queryFn: () => analyticsApi.summary(utcRange), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "trend", utcRange], queryFn: () => analyticsApi.trend(utcRange, "Monthly"), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "top-products", utcRange], queryFn: () => analyticsApi.topProducts(utcRange, "Revenue", 5), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "categories", utcRange], queryFn: () => analyticsApi.categories(utcRange), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "returns", utcRange], queryFn: () => analyticsApi.returns(utcRange), placeholderData: keepPreviousData },
  ] });
  const [summary, trend, topProducts, categories, returns] = queries; const loading = queries.some((query) => query.isLoading); const fetching = queries.some((query) => query.isFetching); const failed = queries.find((query) => query.isError);
  const maxProductRevenue = Math.max(...(topProducts.data?.items.map((item) => item.revenue) ?? []), 1);

  return <div className="mx-auto max-w-[1500px]">
    <div className="page-header"><div className="page-heading"><p className="eyebrow">Satış komuta alanı</p><h1>Satışın bugünkü resmi.</h1><p>Gelirin yönünü, ürünlerin ritmini ve iadelerin etkisini tek bakışta okuyun.</p></div><form className="filter-bar sm:grid-cols-[145px_145px_auto]" onSubmit={(event) => { event.preventDefault(); setRange(draft); }}><label className="field-label">Başlangıç<input className="input mt-1" type="date" value={draft.from} max={draft.to} onChange={(event) => setDraft((current) => ({ ...current, from: event.target.value }))} /></label><label className="field-label">Bitiş<input className="input mt-1" type="date" value={draft.to} min={draft.from} onChange={(event) => setDraft((current) => ({ ...current, to: event.target.value }))} /></label><div className="flex items-end"><button className="btn btn-primary h-[42px]" type="submit">Görünümü yenile</button></div></form></div>
    {loading && <LoadingState label="Satış görünümü hazırlanıyor" />}
    {failed && <ErrorState message={errorMessage(failed.error)} onRetry={() => queries.forEach((query) => void query.refetch())} />}
    {!loading && !failed && summary.data && returns.data && <motion.div animate={{ opacity: fetching ? .5 : 1, y: fetching ? 4 : 0 }} transition={{ duration: .2 }} aria-busy={fetching}>
      {fetching && <div className="fetch-skeleton" />}
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard label="Net satış geliri" value={summary.data.totalRevenue} format={(value) => currency.format(value)} detail={`${summary.data.orderCount} tamamlanmış sipariş`} tone="primary" data={trend.data?.revenue.map((item) => item.value) ?? []} delay={.04} />
        <KpiCard label="Satılan ürün" value={summary.data.unitsSold} format={(value) => integer.format(Math.round(value))} detail={`Ortalama sipariş ${currency.format(summary.data.averageOrderValue)}`} tone="success" data={trend.data?.unitsSold.map((item) => item.value) ?? []} delay={.1} />
        <KpiCard label="Sipariş ritmi" value={summary.data.orderCount} format={(value) => integer.format(Math.round(value))} detail="Tamamlanmış satış adedi" tone="violet" data={trend.data?.orderCount.map((item) => item.value) ?? []} delay={.16} />
        <KpiCard label="İade oranı" value={returns.data.returnRatePercentage} format={(value) => `%${decimal.format(value)}`} detail={`${integer.format(returns.data.returnedQuantity)} adet iade`} tone="warning" data={returns.data.reasons.map((item) => item.quantity)} delay={.22} />
      </section>

      <section className="mt-3 grid gap-3 xl:grid-cols-12">
        <article className="panel p-5 sm:p-6 xl:col-span-8"><div className="panel-heading"><div><p className="eyebrow">Satış eğilimi</p><h2>Aylık gelir hareketi</h2></div>{summary.data.revenueComparison.percentageChange != null && <motion.span className={`trend-pill ${summary.data.revenueComparison.percentageChange >= 0 ? "trend-up" : "trend-down"}`} initial={reduced ? false : { scale: .8, opacity: 0 }} animate={{ scale: 1, opacity: 1 }} transition={{ type: "spring", stiffness: 450, damping: 30 }}>{summary.data.revenueComparison.percentageChange >= 0 ? <ArrowUpRight size={13} /> : <ArrowDownRight size={13} />}%{decimal.format(Math.abs(summary.data.revenueComparison.percentageChange))}</motion.span>}</div>{trend.data?.revenue.length ? <div className="mt-5 h-[340px]"><ResponsiveContainer width="100%" height="100%"><AreaChart data={trend.data.revenue}><defs><linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={chartTheme.primary} stopOpacity={.24} /><stop offset="100%" stopColor={chartTheme.primary} stopOpacity={0} /></linearGradient></defs><CartesianGrid stroke={chartTheme.grid} vertical={false} /><XAxis dataKey="label" stroke={chartTheme.axis} tickLine={false} axisLine={false} tick={{ fontSize: 10 }} /><YAxis stroke={chartTheme.axis} tickLine={false} axisLine={false} tick={{ fontSize: 10 }} tickFormatter={(value) => `${Math.round(value / 1000)}K`} /><Tooltip cursor={{ stroke: chartTheme.grid, strokeDasharray: "4 4" }} content={<ChartTooltip valueFormatter={(value) => currency.format(value)} />} /><Area type="monotone" dataKey="value" name="Gelir" stroke={chartTheme.primary} strokeWidth={2.6} fill="url(#revenueFill)" activeDot={{ r: 5, strokeWidth: 3, stroke: chartTheme.surface, fill: chartTheme.primary }} isAnimationActive={!reduced} animationDuration={900} /></AreaChart></ResponsiveContainer></div> : <EmptyState />}</article>
        <article className="panel overflow-hidden p-5 sm:p-6 xl:col-span-4"><div className="panel-heading"><div><p className="eyebrow">Gelir karması</p><h2>Kategori dağılımı</h2></div></div>{categories.data?.items.length ? <><div className="relative mx-auto mt-3 h-[220px] max-w-[280px]"><ResponsiveContainer width="100%" height="100%"><PieChart><Pie data={categories.data.items.slice(0, 6)} dataKey="revenue" nameKey="name" innerRadius={62} outerRadius={88} paddingAngle={3} cornerRadius={7} isAnimationActive={!reduced} animationDuration={850}>{categories.data.items.slice(0, 6).map((item, index) => <Cell key={item.categoryId} fill={chartColors[index % chartColors.length]} stroke="transparent" />)}</Pie><Tooltip content={<ChartTooltip valueFormatter={(value) => currency.format(value)} />} /></PieChart></ResponsiveContainer><div className="absolute inset-0 grid place-items-center text-center pointer-events-none"><div><p className="eyebrow">Toplam</p><p className="metric-total">{currency.format(categories.data.totalRevenue)}</p></div></div></div><div className="grid grid-cols-2 gap-x-3 gap-y-2">{categories.data.items.slice(0, 6).map((item, index) => <div className="chart-legend-item" key={item.categoryId}><i style={{ background: chartColors[index % chartColors.length] }} /><span className="truncate">{item.name}</span><strong>%{decimal.format(item.revenueSharePercentage)}</strong></div>)}</div></> : <div className="mt-5"><EmptyState /></div>}</article>
      </section>

      <section className="mt-3 grid gap-3 lg:grid-cols-2">
        <article className="panel p-5 sm:p-6"><div className="panel-heading"><div><p className="eyebrow">Ürün performansı</p><h2>Gelire göre liderler</h2></div></div>{topProducts.data?.items.length ? <div className="mt-5 space-y-4">{topProducts.data.items.map((product, index) => <motion.div key={product.productId} className="flex items-center gap-3" initial={reduced ? false : { opacity: 0, x: -10 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: index * .06 }}><span className="rank-simple">{product.rank}.</span><div className="min-w-0 flex-1"><div className="flex items-baseline justify-between gap-3"><p className="table-item-title truncate">{product.name}</p><span className="numeric-value">{currency.format(product.revenue)}</span></div><div className="progress-track mt-2"><motion.div className="progress-primary" initial={{ width: 0 }} animate={{ width: `${Math.max(7, product.revenue / maxProductRevenue * 100)}%` }} transition={{ duration: reduced ? 0 : .75, delay: index * .06, ease: easeOutExpo }} /></div><p className="table-meta">{product.sku}, {integer.format(product.quantity)} adet</p></div></motion.div>)}</div> : <div className="mt-5"><EmptyState /></div>}</article>
        <article className="panel p-5 sm:p-6"><div className="panel-heading"><div><p className="eyebrow">İade görünümü</p><h2>En sık nedenler</h2></div></div>{returns.data.reasons.length ? <div className="mt-4">{returns.data.reasons.slice(0, 5).map((reason, index) => <motion.div key={reason.reason} className="list-row" initial={reduced ? false : { opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * .05 }}><div><p className="table-item-title">{reason.reason}</p><p className="table-meta">{reason.returnCount} kayıt, {reason.quantity} adet</p></div><span className="numeric-value warning-text">{currency.format(reason.amount)}</span></motion.div>)}</div> : <div className="mt-5"><EmptyState title="İade kaydı yok" description="Seçili tarih aralığında iade bulunmuyor." /></div>}</article>
      </section>
    </motion.div>}
  </div>;
}
