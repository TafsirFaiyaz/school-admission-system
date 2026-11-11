export interface AdmissionClass {
  id: string; name: string; totalSeats: number;
  heldSeats: number; confirmedSeats: number; availableSeats: number;
}
export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';
export interface StudentHold {
  holdId: string; classId: string; className: string;
  status: HoldStatus; expiresAt: string;
}
export interface CreatedHold {
  holdId: string; classId: string; studentId: string;
  status: HoldStatus; expiresAt: string;
}
interface ApiErrorResponse { code?: string; message?: string; title?: string; }
const baseUrl = (import.meta.env.VITE_API_BASE_URL || '/backend').replace(/\/$/, '');

export async function request<T>(path: string, options: {
  method?: string; studentId?: string; body?: unknown;
} = {}): Promise<T> {
  const response = await fetch(`${baseUrl}${path}`, {
    cache: 'no-store', // avoid showing stale seat counts or application status
    method: options.method || 'GET',
    headers: {
      ...(options.body !== undefined ? { 'Content-Type': 'application/json' } : {}),
      ...(options.studentId ? { 'X-Student-Id': options.studentId } : {})
    },
    body: options.body === undefined ? undefined : JSON.stringify(options.body)
  });
  if (!response.ok) {
    let details: ApiErrorResponse = {};
    try { details = await response.json() as ApiErrorResponse; } catch { /* non-JSON response */ }
    throw new Error(details.message || details.title || `Request failed (${response.status}).`);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}
