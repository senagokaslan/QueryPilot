import type { ProblemDetails } from "../types/api";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5199").replace(/\/$/, "");

export class ApiError extends Error {
  constructor(public readonly problem: ProblemDetails) {
    super(problem.detail || problem.title || "İstek tamamlanamadı.");
    this.name = "ApiError";
  }
}

export function toQuery<T extends object>(params: T): string {
  const query = new URLSearchParams();
  Object.entries(params as Record<string, unknown>).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") query.set(key, String(value));
  });
  const value = query.toString();
  return value ? `?${value}` : "";
}

export async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      Accept: "application/json",
      ...(options?.body ? { "Content-Type": "application/json" } : {}),
      ...options?.headers,
    },
  });

  if (!response.ok) {
    let problem: ProblemDetails = { title: "İstek başarısız", status: response.status };
    try {
      problem = { ...problem, ...(await response.json()) };
    } catch {
      // Non-JSON failures still use the shared fallback above.
    }
    throw new ApiError(problem);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const validation = error.problem.errors ? Object.values(error.problem.errors).flat()[0] : undefined;
    return validation || error.message;
  }
  if (error instanceof TypeError) return "Backend bağlantısı kurulamadı. API adresini ve HTTPS sertifikasını kontrol edin.";
  return error instanceof Error ? error.message : "Beklenmeyen bir hata oluştu.";
}
