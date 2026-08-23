import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Power } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { toast } from "sonner";
import { z } from "zod";
import { categoriesApi } from "../api/resources";
import { errorMessage } from "../api/client";
import { EmptyState, ErrorState, LoadingState } from "../components/feedback";
import { MotionTableBody, MotionTableRow } from "../components/motion";
import { ActiveBadge, CollectionSummary, FormError, Modal, PageHeader, Pagination, SubmitButton, TableShell } from "../components/ui";
import { formatDate } from "../lib/format";
import type { Category } from "../types/api";

const schema = z.object({ name: z.string().trim().min(1, "Kategori adı zorunludur.").max(100), isActive: z.boolean() });
type FormValues = z.infer<typeof schema>;

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [activeFilter, setActiveFilter] = useState("");
  const [editing, setEditing] = useState<Category | null | undefined>(undefined);
  const query = useQuery({ queryKey: ["categories", page, activeFilter], queryFn: () => categoriesApi.list({ page, pageSize: 20, isActive: activeFilter === "" ? undefined : activeFilter === "true" }), placeholderData: (previous) => previous });
  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { name: "", isActive: true } });
  const close = () => { setEditing(undefined); form.reset({ name: "", isActive: true }); };
  const openCreate = () => { form.reset({ name: "", isActive: true }); setEditing(null); };
  const openEdit = (item: Category) => { form.reset({ name: item.name, isActive: item.isActive }); setEditing(item); };
  const save = useMutation({
    mutationFn: (values: FormValues) => editing ? categoriesApi.update(editing.id, values) : categoriesApi.create({ name: values.name }),
    onSuccess: async () => { toast.success(editing ? "Kategori güncellendi." : "Kategori oluşturuldu."); close(); await queryClient.invalidateQueries({ queryKey: ["categories"] }); },
    onError: (error) => toast.error(errorMessage(error)),
  });
  const deactivate = useMutation({ mutationFn: categoriesApi.deactivate, onSuccess: async () => { toast.success("Kategori pasifleştirildi."); await queryClient.invalidateQueries({ queryKey: ["categories"] }); }, onError: (error) => toast.error(errorMessage(error)) });

  return <div className="mx-auto max-w-[1400px]"><PageHeader eyebrow="Katalog" title="Kategoriler" description="Ürün kataloğunuzun temel gruplarını yönetin. Silme işlemi tarihsel veriyi korumak için pasifleştirir." action={<button className="btn btn-primary" onClick={openCreate}><Plus size={16} />Yeni kategori</button>} />
    <div className="filter-bar ml-auto max-w-[280px]"><label className="field-label">Durum görünümü<select className="select mt-1" value={activeFilter} onChange={(event) => { setActiveFilter(event.target.value); setPage(1); }}><option value="">Tüm kategoriler</option><option value="true">Yalnız aktif</option><option value="false">Yalnız pasif</option></select></label></div>
    {query.isLoading ? <LoadingState /> : query.isError ? <ErrorState message={errorMessage(query.error)} onRetry={() => void query.refetch()} /> : !query.data?.items.length ? <EmptyState title="Kategori bulunamadı" description="Filtreyi değiştirin veya ilk kategorinizi oluşturun." /> : <><CollectionSummary items={[{ label: "Toplam kayıt", value: query.data.totalCount, detail: "Seçili görünüm" }, { label: "Bu sayfada aktif", value: query.data.items.filter((item) => item.isActive).length, tone: "sage" }, { label: "Bu sayfada pasif", value: query.data.items.filter((item) => !item.isActive).length, tone: "lilac" }]} /><TableShell busy={query.isFetching}><table className="data-table"><thead><tr><th>Kategori</th><th>Durum</th><th>Oluşturma</th><th className="text-right!">İşlem</th></tr></thead><MotionTableBody refreshKey={`${page}-${activeFilter}`}>{query.data.items.map((item) => <MotionTableRow key={item.id}><td><p className="table-item-title">{item.name}</p><p className="table-meta">#{item.id}</p></td><td><ActiveBadge active={item.isActive} /></td><td>{formatDate(item.createdAt)}</td><td><div className="flex justify-end gap-2"><button className="icon-btn" aria-label={`${item.name} kategorisini düzenle`} onClick={() => openEdit(item)}><Pencil size={15} /></button>{item.isActive && <button className="icon-btn warning-text" aria-label={`${item.name} kategorisini pasifleştir`} onClick={() => { if (window.confirm(`${item.name} kategorisi pasifleştirilsin mi?`)) deactivate.mutate(item.id); }}><Power size={15} /></button>}</div></td></MotionTableRow>)}</MotionTableBody></table></TableShell><Pagination page={query.data.page} totalPages={query.data.totalPages} totalCount={query.data.totalCount} onPage={setPage} /></>}
    {editing !== undefined && <Modal title={editing ? "Kategoriyi düzenle" : "Yeni kategori"} onClose={close}><form className="space-y-4" onSubmit={form.handleSubmit((values) => save.mutate(values))}><label className="field-label block">Kategori adı<input className="input mt-1" {...form.register("name")} /></label><FormError message={form.formState.errors.name?.message} />{editing && <label className="form-check"><input className="checkbox" type="checkbox" {...form.register("isActive")} />Aktif kategori</label>}<div className="flex justify-end gap-2 pt-2"><button className="btn btn-secondary" type="button" onClick={close}>Vazgeç</button><SubmitButton pending={save.isPending}>Kaydet</SubmitButton></div></form></Modal>}
  </div>;
}
