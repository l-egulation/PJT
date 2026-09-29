export type AccountIdentity = { accountId: string; characterId: string; email: string; nickname: string; level: number; experience: number; rice: number };
export type SessionState = { authenticated: boolean; account: AccountIdentity | null };
export type Credentials = { email: string; password: string };
export type SignupCredentials = Credentials & { nickname: string };
export type NicknameUpdate = { nickname: string };
export type PasswordResetRequest = { email: string };
export type PasswordResetConfirmation = { token: string; password: string };
export type PasswordResetAcknowledgement = { accepted: boolean };
export type PasswordChange = { currentPassword: string; newPassword: string; newPasswordConfirmation: string };
export type SocialProvider = { id: string; displayName: string; enabled: boolean };
export type SocialAuthorization = { authorizationUrl: string };
export type PendingSocialSignup = { token: string; provider: string; email: string | null; suggestedNickname: string | null };
export type SocialConnection = { provider: string; displayName: string; email: string | null };
export type SocialConnections = { passwordEnabled: boolean; providers: SocialConnection[] };
export type SocialSignup = { token: string; nickname: string };

type Envelope<T> = { data: T };
type ErrorDetail = { field?: string; code: string; messageKey: string; value?: unknown };
type ErrorEnvelope = { code?: string; messageKey?: string; details?: ErrorDetail[] | null };

export class AuthApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function json<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => ({})) as Envelope<T> & ErrorEnvelope;
  if (!response.ok) throw new AuthApiError(response.status, body.code || `HTTP ${response.status}`);
  return body.data;
}

function commandHeaders(): HeadersInit {
  return { "Accept": "application/json", "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() };
}

export const authApi = {
  socialProviders: () => fetch("/api/v1/auth/social/providers", { credentials: "include", headers: { Accept: "application/json" } }).then(json<SocialProvider[]>),
  beginSocial: (provider: string, link = false) => fetch(`/api/v1/auth/social/${encodeURIComponent(provider)}/${link ? "link/" : ""}begin`, {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: "{}",
  }).then(json<SocialAuthorization>),
  pendingSocialSignup: () => fetch("/api/v1/auth/social/pending", { credentials: "include", headers: { Accept: "application/json" } }).then(json<PendingSocialSignup>),
  finishSocialSignup: (request: SocialSignup) => fetch("/api/v1/auth/social/signup", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: JSON.stringify(request),
  }).then(json<AccountIdentity>),
  socialConnections: () => fetch("/api/v1/auth/social/connections", { credentials: "include", headers: { Accept: "application/json" } }).then(json<SocialConnections>),
  unlinkSocial: (provider: string) => fetch(`/api/v1/auth/social/connections/${encodeURIComponent(provider)}`, {
    method: "DELETE",
    credentials: "include",
    headers: { Accept: "application/json", "Idempotency-Key": crypto.randomUUID() },
  }).then(json<SocialConnections>),
  session: () => fetch("/api/v1/auth/session", { credentials: "include", headers: { Accept: "application/json" } }).then(json<SessionState>),
  signup: (credentials: SignupCredentials) => fetch("/api/v1/auth/signup", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: JSON.stringify(credentials),
  }).then(json<AccountIdentity>),
  login: (credentials: Credentials) => fetch("/api/v1/auth/login", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: JSON.stringify(credentials),
  }).then(json<AccountIdentity>),
  requestPasswordReset: (request: PasswordResetRequest) => fetch("/api/v1/auth/password-reset/request", {
    method: "POST",
    credentials: "include",
    headers: { "Accept": "application/json", "Content-Type": "application/json" },
    body: JSON.stringify(request),
  }).then(json<PasswordResetAcknowledgement>),
  confirmPasswordReset: (request: PasswordResetConfirmation) => fetch("/api/v1/auth/password-reset/confirm", {
    method: "POST",
    credentials: "include",
    headers: { "Accept": "application/json", "Content-Type": "application/json" },
    body: JSON.stringify(request),
  }).then(json<PasswordResetAcknowledgement>),
  updateNickname: (request: NicknameUpdate) => fetch("/api/v1/auth/profile/nickname", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: JSON.stringify(request),
  }).then(json<AccountIdentity>),
  changePassword: (request: PasswordChange) => fetch("/api/v1/auth/profile/password", {
    method: "POST",
    credentials: "include",
    headers: commandHeaders(),
    body: JSON.stringify(request),
  }).then(json<SessionState>),
  logout: () => fetch("/api/v1/auth/logout", {
    method: "POST",
    credentials: "include",
    headers: { Accept: "application/json", "Idempotency-Key": crypto.randomUUID() },
  }).then(json<SessionState>),
  deleteAccount: () => fetch("/api/v1/auth/delete", {
    method: "POST",
    credentials: "include",
    headers: { Accept: "application/json", "Idempotency-Key": crypto.randomUUID() },
  }).then(json<SessionState>),
};
