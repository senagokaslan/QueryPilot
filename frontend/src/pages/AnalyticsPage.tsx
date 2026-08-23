import { keepPreviousData, useQueries, useQuery } from "@tanstack/react-query";
import { motion, useReducedMotion } from "framer-motion";
import { useMemo, useState } from "react";
import { Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { analyticsApi } from "../api/analytics";
import { errorMessage } from "../api/client";
import { categoriesApi } from "../api/resources";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback";
import { AnimatedNumber, MotionTableBody, MotionTableRow, easeOutExpo } from "../components/motion";
import { ChartTooltip, PageHeader, TableShell } from "../components/ui";
import { currency, dateInputValue, decimal, integer, toUtcExclusiveEnd, toUtcStart } from "../lib/format";
import { chartTheme } from "../lib/theme";
import type { AnalyticsGranularity, TopProductsMetric } from "../types/api";

function initialRange() {
  const to = new Date();
  const from = new Date();
  from.setMonth(from.getMonth() - 6);
  return { from: dateInputValue(from), to: dateInputValue(to) };
}

export function AnalyticsPage() {
  const reduced = useReducedMotion();
  const [draft, setDraft] = useState(initialRange);
  const [range, setRange] = useState(initialRange);
  const [granularity, setGranularity] = useState<AnalyticsGranularity>("Monthly");
  const [metric, setMetric] = useState<TopProductsMetric>("Revenue");
  const [limit, setLimit] = useState(10);
  const [limitInput, setLimitInput] = useState("10");
  const [categoryId, setCategoryId] = useState("");
  const utcRange = useMemo(() => ({ from: toUtcStart(range.from), to: toUtcExclusiveEnd(range.to) }), [range]);
  const categoryOptions = useQuery({ queryKey: ["categories", "analytics-options"], queryFn: () => categoriesApi.list({ pageSize: 100 }) });
  const queries = useQueries({ queries: [
    { queryKey: ["analytics", "summary", utcRange], queryFn: () => analyticsApi.summary(utcRange), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "trend", utcRange, granularity], queryFn: () => analyticsApi.trend(utcRange, granularity), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "top", utcRange, metric, limit, categoryId], queryFn: () => analyticsApi.topProducts(utcRange, metric, limit, categoryId ? Number(categoryId) : undefined), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "categories", utcRange], queryFn: () => analyticsApi.categories(utcRange), placeholderData: keepPreviousData },
    { queryKey: ["analytics", "returns", utcRange], queryFn: () => analyticsApi.returns(utcRange), placeholderData: keepPreviousData },
  ] });
  const [summary, trend, top, categories, returns] = queries;
  const loading = queries.some((query) => query.isLoading);
  const fetching = queries.some((query) => query.isFetching);
  const mainFetching = [summary, trend, categories, returns].some((query) => query.isFetching);
  const failed = queries.find((query) => query.isError);
  const maxTop = Math.max(...(top.data?.items.map((item) => metric === "Revenue" ? item.revenue : item.quantity) ?? []), 1);
  const granularityLabel = { Daily: "Günlük", Weekly: "Haftalık", Monthly: "Aylık" }[granularity];
  const trendData = trend.data?.revenue.map((point, index) => ({
    label: point.label,
    revenue: point.value,
    orders: trend.data?.orderCount[index]?.value ?? 0,
    units: trend.data?.unitsSold[index]?.value ?? 0,
  })) ?? [];

  const commitLimit = () => {
    if (!limitInput.trim()) {
      setLimitInput(String(limit));
      return;
    }
    const next = Math.min(50, Math.max(1, Number(limitInput)));
    setLimitInput(String(next));
    setLimit(next);
  };

  return <div className="mx-auto max-w-[1500px]">
    <PageHeader eyebrow="Analiz stüdyosu" title="Derin analiz" description="Satışın hareketini farklı zaman ve ürün mercekleriyle karşılaştırın; tüm hesaplamalar gerçek API sonuçlarından gelir." />
    <form className="filter-bar md:grid-cols-3 xl:grid-cols-[1fr_1fr_160px_170px_210px_auto]" onSubmit={(event) => { event.preventDefault(); commitLimit(); setRange(draft); }}>
      <label className="field-label">Başlangıç<input className="input mt-1" type="date" value={draft.from} max={draft.to} onChange={(event) => setDraft((value) => ({ ...value, from: event.target.value }))} /></label>
      <label className="field-label">Bitiş<input className="input mt-1" type="date" value={draft.to} min={draft.from} onChange={(event) => setDraft((value) => ({ ...value, to: event.target.value }))} /></label>
      <label className="field-label">Trend<select className="select mt-1" value={granularity} onChange={(event) => setGranularity(event.target.value as AnalyticsGranularity)}><option value="Daily">Günlük</option><option value="Weekly">Haftalık</option><option value="Monthly">Aylık</option></select></label>
      <label className="field-label">Ürün metriği<select className="select mt-1" value={metric} onChange={(event) => setMetric(event.target.value as TopProductsMetric)}><option value="Revenue">Gelir</option><option value="Quantity">Adet</option></select></label>
      <label className="field-label">Kategori<select className="select mt-1" value={categoryId} onChange={(event) => setCategoryId(event.target.value)}><option value="">Tüm kategoriler</option>{categoryOptions.data?.items.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <div className="flex items-end"><button className="btn btn-primary h-[42px] w-full" type="submit">Analizi uygula</button></div>
    </form>

    {loading ? <LoadingState label="Analiz yüzeyi hazırlanıyor" /> : failed ? <ErrorState message={errorMessage(failed.error)} onRetry={() => queries.forEach((query) => void query.refetch())} /> : summary.data && trend.data && top.data && categories.data && returns.data ? <motion.div animate={{ opacity: mainFetching ? .48 : 1, y: mainFetching ? 4 : 0 }} transition={{ duration: .2 }} aria-busy={fetching}>
      {mainFetching && <div className="fetch-skeleton" />}
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">{[
        { label: "Net gelir", value: summary.data.totalRevenue, format: (value: number) => currency.format(value) },
        { label: "Sipariş", value: summary.data.orderCount, format: (value: number) => integer.format(Math.round(value)) },
        { label: "Satılan adet", value: summary.data.unitsSold, format: (value: number) => integer.format(Math.round(value)) },
        { label: "İade tutarı", value: returns.data.totalReturnAmount, format: (value: number) => currency.format(value) },
        { label: "İade oranı", value: returns.data.returnRatePercentage, format: (value: number) => `%${decimal.format(value)}` },
      ].map((item, index) => <motion.article className="panel kpi-card" key={item.label} initial={reduced ? false : { opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * .055, ease: easeOutExpo }}><p className="eyebrow">{item.label}</p><p className="kpi-value">{item.value == null ? "Veri yok" : <AnimatedNumber value={item.value} format={item.format} />}</p></motion.article>)}</section>

      <section className="mt-3 grid gap-3 xl:grid-cols-[1.55fr_1fr]">
        <article className="panel p-5 sm:p-6">
          <div className="panel-heading"><div><p className="eyebrow">Satış eğilimi</p><h2>{granularityLabel} satış görünümü</h2></div><span className="intent-chip">{trendData.length} dönem</span></div>
          {trendData.length ? <div className="analytics-chart-stack">
            <section className="analytics-revenue-chart">
              <div className="chart-subheading"><div><span>Gelir</span><strong>{currency.format(summary.data.totalRevenue)}</strong></div><small>Ana finansal ölçek</small></div>
              <div className="h-[270px]"><ResponsiveContainer width="100%" height="100%"><AreaChart data={trendData} margin={{ top: 12, right: 8, left: 0, bottom: 0 }}><defs><linearGradient id="analyticsRevenueFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor={chartTheme.primary} stopOpacity={.28} /><stop offset="1" stopColor={chartTheme.primary} stopOpacity={0} /></linearGradient></defs><CartesianGrid stroke={chartTheme.grid} vertical={false} strokeDasharray="4 4" /><XAxis dataKey="label" stroke={chartTheme.axis} tick={{ fontSize: 9 }} axisLine={false} tickLine={false} /><YAxis stroke={chartTheme.axis} tick={{ fontSize: 9 }} axisLine={false} tickLine={false} tickFormatter={(value) => `${Math.round(value / 1000)}K`} /><Tooltip cursor={{ stroke: chartTheme.grid, strokeDasharray: "4 4" }} content={<ChartTooltip valueFormatter={(value) => currency.format(value)} />} /><Area type="monotone" dataKey="revenue" name="Gelir" stroke={chartTheme.primary} fill="url(#analyticsRevenueFill)" strokeWidth={2.7} activeDot={{ r: 5, fill: chartTheme.primary, stroke: chartTheme.surface, strokeWidth: 3 }} isAnimationActive={!reduced} animationDuration={900} /></AreaChart></ResponsiveContainer></div>
            </section>
            <div className="analytics-mini-grid">
              <section className="analytics-mini-chart">
                <div className="chart-subheading"><div><span>Sipariş sayısı</span><strong>{integer.format(summary.data.orderCount)}</strong></div><small>Kendi ölçeğinde</small></div>
                <div className="h-[150px]"><ResponsiveContainer width="100%" height="100%"><BarChart data={trendData} margin={{ top: 10, right: 4, left: -20, bottom: 0 }}><CartesianGrid stroke={chartTheme.grid} vertical={false} strokeDasharray="4 4" /><XAxis dataKey="label" stroke={chartTheme.axis} tick={{ fontSize: 8 }} axisLine={false} tickLine={false} /><YAxis stroke={chartTheme.axis} tick={{ fontSize: 8 }} axisLine={false} tickLine={false} /><Tooltip cursor={{ fill: "var(--violet-soft)" }} content={<ChartTooltip valueFormatter={(value) => integer.format(value)} />} /><Bar dataKey="orders" name="Sipariş" fill={chartTheme.violet} radius={[6, 6, 2, 2]} isAnimationActive={!reduced} animationDuration={750} /></BarChart></ResponsiveContainer></div>
              </section>
              <section className="analytics-mini-chart">
                <div className="chart-subheading"><div><span>Satılan adet</span><strong>{integer.format(summary.data.unitsSold)}</strong></div><small>Kendi ölçeğinde</small></div>
                <div className="h-[150px]"><ResponsiveContainer width="100%" height="100%"><AreaChart data={trendData} margin={{ top: 10, right: 4, left: -20, bottom: 0 }}><defs><linearGradient id="analyticsUnitsFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor={chartTheme.success} stopOpacity={.24} /><stop offset="1" stopColor={chartTheme.success} stopOpacity={0} /></linearGradient></defs><CartesianGrid stroke={chartTheme.grid} vertical={false} strokeDasharray="4 4" /><XAxis dataKey="label" stroke={chartTheme.axis} tick={{ fontSize: 8 }} axisLine={false} tickLine={false} /><YAxis stroke={chartTheme.axis} tick={{ fontSize: 8 }} axisLine={false} tickLine={false} /><Tooltip cursor={{ stroke: chartTheme.grid, strokeDasharray: "4 4" }} content={<ChartTooltip valueFormatter={(value) => integer.format(value)} />} /><Area type="monotone" dataKey="units" name="Adet" stroke={chartTheme.success} fill="url(#analyticsUnitsFill)" strokeWidth={2.2} activeDot={{ r: 4, fill: chartTheme.success, stroke: chartTheme.surface, strokeWidth: 2 }} isAnimationActive={!reduced} animationDuration={800} /></AreaChart></ResponsiveContainer></div>
              </section>
            </div>
          </div> : <div className="mt-5"><EmptyState /></div>}
        </article>

        <motion.article className="panel p-5 sm:p-6" animate={{ opacity: top.isFetching ? .58 : 1 }} transition={{ duration: .18 }} aria-busy={top.isFetching}>
          <div className="flex items-end justify-between gap-4"><div><p className="eyebrow">Ürün sıralaması</p><h2>Öne çıkan ürünler</h2></div><label className="field-label w-20">Limit<input className="input mt-1" type="text" inputMode="numeric" pattern="[0-9]*" maxLength={2} value={limitInput} onChange={(event) => setLimitInput(event.target.value.replace(/\D/g, ""))} onBlur={commitLimit} onKeyDown={(event) => { if (event.key === "Enter") event.currentTarget.blur(); }} /></label></div>
          {top.data.items.length ? <div className="mt-5 space-y-3">{top.data.items.map((item, index) => { const value = metric === "Revenue" ? item.revenue : item.quantity; return <motion.div className="list-card" key={item.productId} initial={reduced ? false : { opacity: 0, x: 10 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: index * .04 }}><div className="flex justify-between gap-3"><div className="min-w-0"><p className="table-item-title truncate"><span className="rank-simple">{item.rank}.</span>{item.name}</p><p className="table-meta">{item.sku}</p></div><div className="text-right"><p className="numeric-value">{currency.format(item.revenue)}</p><p className="table-meta">{item.quantity} adet</p></div></div><div className="progress-track mt-3"><motion.div className="progress-violet" initial={{ width: 0 }} animate={{ width: `${Math.max(5, value / maxTop * 100)}%` }} transition={{ duration: reduced ? 0 : .65, delay: index * .04, ease: easeOutExpo }} /></div></motion.div>; })}</div> : <div className="mt-5"><EmptyState /></div>}
        </motion.article>
      </section>

      <section className="mt-3 grid gap-3 xl:grid-cols-2">
        <article className="panel p-5 sm:p-6"><p className="eyebrow">Kategori performansı</p><h2>Gelir ve pay dağılımı</h2>{categories.data.items.length ? <div className="mt-5 h-[330px]"><ResponsiveContainer width="100%" height="100%"><BarChart data={categories.data.items.slice(0, 10)}><CartesianGrid stroke={chartTheme.grid} vertical={false} /><XAxis dataKey="name" stroke={chartTheme.axis} tick={{ fontSize: 9 }} axisLine={false} tickLine={false} /><YAxis stroke={chartTheme.axis} tick={{ fontSize: 9 }} axisLine={false} tickLine={false} /><Tooltip content={<ChartTooltip valueFormatter={(value) => currency.format(value)} />} /><Bar dataKey="revenue" name="Gelir" fill={chartTheme.success} radius={[8,8,0,0]} isAnimationActive={!reduced} animationDuration={800} /></BarChart></ResponsiveContainer></div> : <EmptyState />}</article>
        <article className="panel p-5 sm:p-6"><p className="eyebrow">İade analizi</p><h2>Neden kırılımı</h2>{returns.data.reasons.length ? <div className="mt-5"><TableShell busy={fetching}><table className="data-table"><thead><tr><th>Neden</th><th>Kayıt</th><th>Adet</th><th>Tutar</th></tr></thead><MotionTableBody refreshKey={range.from + range.to}>{returns.data.reasons.map((reason) => <MotionTableRow key={reason.reason}><td className="table-item-title">{reason.reason}</td><td>{reason.returnCount}</td><td>{reason.quantity}</td><td>{currency.format(reason.amount)}</td></MotionTableRow>)}</MotionTableBody></table></TableShell></div> : <div className="mt-5"><EmptyState title="İade yok" /></div>}</article>
      </section>
    </motion.div> : null}
  </div>;
}
