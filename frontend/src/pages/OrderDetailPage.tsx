import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { motion, useReducedMotion } from "framer-motion";
import { ArrowLeft, Mail, MapPin, RotateCcw } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import { z } from "zod";
import { errorMessage } from "../api/client";
import { ordersApi, returnsApi } from "../api/resources";
import { ErrorState, LoadingState } from "../components/feedback";
import { AnimatedNumber, MotionTableBody, MotionTableRow, easeOutExpo } from "../components/motion";
import { FormError, Modal, OrderStatusBadge, PageHeader, SubmitButton, TableShell } from "../components/ui";
import { currency, formatDate } from "../lib/format";
import type { OrderDetail } from "../types/api";

const schema = z.object({ quantity: z.number().int().positive("Miktar en az 1 olmalıdır."), reason: z.string().trim().min(1, "İade nedeni zorunludur.").max(500) });
type Values = z.infer<typeof schema>;

export function OrderDetailPage() {
  const { id } = useParams(); const orderId = Number(id); const client = useQueryClient(); const reduced = useReducedMotion(); const [returnItem, setReturnItem] = useState<OrderDetail["items"][number] | null>(null);
  const query = useQuery({ queryKey: ["orders", orderId], queryFn: () => ordersApi.get(orderId), enabled: Number.isInteger(orderId) && orderId > 0 });
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { quantity: 1, reason: "" } });
  const mutation = useMutation({ mutationFn: (values: Values) => returnsApi.create({ orderItemId: returnItem!.id, ...values }), onSuccess: async () => { toast.success("İade kaydı oluşturuldu."); setReturnItem(null); form.reset(); await client.invalidateQueries({ queryKey: ["orders", orderId] }); await client.invalidateQueries({ queryKey: ["analytics"] }); }, onError: (error) => toast.error(errorMessage(error)) });
  if (query.isLoading) return <LoadingState label="Sipariş detayı yükleniyor" />;
  if (query.isError || !query.data) return <ErrorState message={errorMessage(query.error)} onRetry={() => void query.refetch()} />;
  const order = query.data; const returnedAmount = order.items.reduce((sum, item) => sum + (item.returnSummary?.returnedAmount ?? 0), 0);
  return <div className="mx-auto max-w-6xl"><PageHeader eyebrow="Sipariş kaydı" title={`Sipariş #${order.id}`} description={`${formatDate(order.orderDate)} tarihinde ${order.customer.name} adına oluşturuldu.`} action={<Link className="btn btn-secondary" to="/orders"><ArrowLeft size={16} />Listeye dön</Link>} />
    <section className="order-detail-hero"><motion.article className="order-total" initial={reduced ? false : { opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ ease: easeOutExpo }}><p className="eyebrow">Sipariş toplamı</p><p><AnimatedNumber value={order.totalAmount} format={(value) => currency.format(value)} /></p><OrderStatusBadge status={order.status} /></motion.article><motion.article className="order-customer" initial={reduced ? false : { opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: .07, ease: easeOutExpo }}><p className="eyebrow">Müşteri profili</p><h2>{order.customer.name}</h2><div><span><Mail size={14} />{order.customer.email}</span><span><MapPin size={14} />{order.customer.city}</span></div></motion.article><motion.article className="order-return" initial={reduced ? false : { opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: .14, ease: easeOutExpo }}><p className="eyebrow">İade görünümü</p><strong><AnimatedNumber value={returnedAmount} format={(value) => currency.format(value)} /></strong><span>{order.items.reduce((sum, item) => sum + (item.returnSummary?.returnedQuantity ?? 0), 0)} adet iade</span></motion.article></section>
    {order.status === "Pending" && <motion.div className="backend-note mb-4" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>Bu sipariş bekliyor. Backend’de durum değiştirme endpointi bulunmadığından frontend üzerinden tamamlanamaz ve iade oluşturulamaz.</motion.div>}
    <TableShell><table className="data-table"><thead><tr><th>Ürün</th><th>Miktar</th><th>Birim fiyat</th><th>Satır toplamı</th><th>İade özeti</th><th /></tr></thead><MotionTableBody>{order.items.map((item) => { const returned = item.returnSummary?.returnedQuantity ?? 0; const remaining = item.quantity - returned; return <MotionTableRow key={item.id}><td><p className="table-item-title">{item.product.name}</p><p className="table-meta">{item.product.sku}, kalem #{item.id}</p></td><td>{item.quantity}</td><td>{currency.format(item.unitPrice)}</td><td className="numeric-value">{currency.format(item.lineTotal)}</td><td>{item.returnSummary ? <div><p>{item.returnSummary.returnedQuantity} adet</p><p className="table-meta">{currency.format(item.returnSummary.returnedAmount)}</p></div> : <span className="caption">İade yok</span>}</td><td>{order.status === "Completed" && remaining > 0 && <button className="btn btn-secondary" onClick={() => { form.reset({ quantity: 1, reason: "" }); setReturnItem(item); }}><RotateCcw size={14} />İade</button>}</td></MotionTableRow>; })}</MotionTableBody></table></TableShell>
    {returnItem && <Modal title="İade oluştur" description={`${returnItem.product.name}, en fazla ${returnItem.quantity - (returnItem.returnSummary?.returnedQuantity ?? 0)} adet`} onClose={() => setReturnItem(null)}><form className="space-y-4" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}><label className="field-label block">Miktar<input className="input mt-1" type="number" min="1" max={returnItem.quantity - (returnItem.returnSummary?.returnedQuantity ?? 0)} {...form.register("quantity", { valueAsNumber: true })} /><FormError message={form.formState.errors.quantity?.message} /></label><label className="field-label block">İade nedeni<textarea className="textarea mt-1 min-h-28 resize-y" maxLength={500} {...form.register("reason")} /><FormError message={form.formState.errors.reason?.message} /></label><div className="flex justify-end gap-2"><button className="btn btn-secondary" type="button" onClick={() => setReturnItem(null)}>Vazgeç</button><SubmitButton pending={mutation.isPending}>İadeyi kaydet</SubmitButton></div></form></Modal>}
  </div>;
}
