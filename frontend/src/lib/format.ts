export const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 2 });
export const integer = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 0 });
export const decimal = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 2 });

export function formatDate(value: string): string {
  return new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

export function toUtcStart(date: string): string {
  return new Date(`${date}T00:00:00`).toISOString();
}

export function toUtcExclusiveEnd(date: string): string {
  const end = new Date(`${date}T00:00:00`);
  end.setDate(end.getDate() + 1);
  return end.toISOString();
}

export function toUtcInclusiveEnd(date: string): string {
  const end = new Date(`${date}T23:59:59.999`);
  return end.toISOString();
}

export function dateInputValue(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 10);
}
