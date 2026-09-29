import type { FormEvent, ReactNode } from "react";
import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useBattleRuntimeStore } from "../battle/runtimeStore";
import { gameSessionClient } from "../runtime/gameSessionClient";
import { authApi, AuthApiError, type AccountIdentity, type Credentials, type SignupCredentials } from "./api";
import { LoadingScene } from "../../shared/LoadingScene";
import brandLogo from "./assets-cozy-pixel/auth-brand-logo.png";
import emailIcon from "./assets-cozy-pixel/auth-field-email-icon.png";
import lockIcon from "./assets-cozy-pixel/auth-field-lock-icon.png";
import userIcon from "./assets-cozy-pixel/auth-field-user-icon.png";
import passwordHiddenImage from "./assets/auth-password-hidden.png";
import passwordVisibleImage from "./assets/auth-password-visible.png";
import { DeploymentStatusScreen } from "./DeploymentStatusScreen";
import { SocialLoginPanel } from "./SocialLoginPanel";
import "./AuthGate.css";

const AUTH_ERRORS: Record<string, string> = {
  INVALID_EMAIL: "올바른 이메일 주소를 입력해 주세요.",
  EMAIL_ALREADY_EXISTS: "이미 가입된 이메일입니다.",
  LOGIN_FAILED: "이메일 또는 비밀번호를 확인해 주세요.",
  PASSWORD_TOO_SHORT: "비밀번호는 8자 이상이어야 합니다.",
  PASSWORD_RESET_TOKEN_INVALID: "재설정 링크가 만료되었거나 이미 사용되었습니다.",
  IDEMPOTENCY_KEY_REUSED: "요청이 충돌했습니다. 다시 시도해 주세요.",
  NICKNAME_REQUIRED: "닉네임을 입력해 주세요.",
  NICKNAME_TOO_LONG: "닉네임은 20자 이하로 입력해 주세요.",
};

/** portal = 소셜 우선 진입 화면, login/signup = 이메일 폼 */
type AuthMode = "portal" | "login" | "signup" | "forgot" | "reset";

function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message in AUTH_ERRORS) return AUTH_ERRORS[error.message];
  return "요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
}

export function AuthGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const resetBattleRuntime = useBattleRuntimeStore((state) => state.reset);
  const [passwordResetToken] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get("token") ?? "");
  useEffect(() => {
    if (passwordResetToken) window.history.replaceState({}, "", window.location.pathname);
  }, [passwordResetToken]);
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false });
  const logout = useMutation({
    mutationFn: authApi.logout,
    onSuccess: () => {
      queryClient.setQueryData(["auth", "session"], { authenticated: false, account: null });
      gameSessionClient.invalidate();
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "auth" });
      resetBattleRuntime();
    },
  });


  const acceptAccount = (account: AccountIdentity) => {
    queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "auth" });
    queryClient.setQueryData(["auth", "session"], { authenticated: true, account });
  };
  if (session.isError && session.error instanceof Error && (session.error instanceof TypeError || (session.error instanceof AuthApiError && session.error.status >= 500))) {
    return <DeploymentStatusScreen />;
  }

  if (session.isLoading && !passwordResetToken) return <LoadingScene />;

  if (passwordResetToken) {
    return <AuthForm onAuthenticated={acceptAccount} sessionFailed={session.isError} initialResetToken={passwordResetToken} />;
  }

  if (session.data?.authenticated && session.data.account) {
    return <>
      <div className="auth-bar" aria-label="현재 로그인 정보">
        <span className="auth-account"><strong>{session.data.account.nickname}</strong><small>{session.data.account.email}</small></span>
        <button type="button" disabled={logout.isPending} onClick={() => logout.mutate()}>{logout.isPending ? "로그아웃 중" : "로그아웃"}</button>
      </div>
      {logout.error && <p className="auth-inline-error" role="alert">로그아웃하지 못했습니다. 다시 시도해 주세요.</p>}
      {children}
    </>;
  }

  return <AuthForm onAuthenticated={acceptAccount} sessionFailed={session.isError} initialResetToken="" />;
}

function AuthForm({
  onAuthenticated,
  sessionFailed,
  initialResetToken,
}: {
  onAuthenticated: (account: AccountIdentity) => void;
  sessionFailed: boolean;
  initialResetToken: string;
}) {
  const [mode, setMode] = useState<AuthMode>(initialResetToken ? "reset" : "portal");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordConfirmation, setPasswordConfirmation] = useState("");
  const [nickname, setNickname] = useState("");
  const [passwordVisible, setPasswordVisible] = useState(false);
  const loginCredentials: Credentials = { email: email.trim(), password };
  const signupCredentials: SignupCredentials = { ...loginCredentials, nickname: nickname.trim() };
  const mutation = useMutation({
    mutationFn: () => mode === "login" ? authApi.login(loginCredentials) : authApi.signup(signupCredentials),
    onSuccess: onAuthenticated,
  });
  const resetMutation = useMutation({
    mutationFn: () => mode === "forgot"
      ? authApi.requestPasswordReset({ email: email.trim() })
      : authApi.confirmPasswordReset({ token: initialResetToken, password }),
  });
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (mode === "reset" && password !== passwordConfirmation) return;
    if (mode === "forgot" || mode === "reset") resetMutation.mutate();
    else mutation.mutate();
  };

  const selectMode = (nextMode: Exclude<AuthMode, "reset">) => {
    if (nextMode === mode) return;
    setMode(nextMode);
    setPassword("");
    setPasswordConfirmation("");
    setPasswordVisible(false);
    mutation.reset();
    resetMutation.reset();
  };

  const passwordsDiffer = mode === "reset" && passwordConfirmation.length > 0 && password !== passwordConfirmation;
  const feedback = resetMutation.error
    ? errorMessage(resetMutation.error)
    : resetMutation.isSuccess
      ? mode === "forgot"
        ? "가입 여부와 관계없이 입력한 주소로 재설정 안내를 요청했습니다."
        : "비밀번호를 변경했습니다. 새 비밀번호로 로그인해 주세요."
      : passwordsDiffer
        ? "새 비밀번호가 서로 일치하지 않습니다."
        : mutation.error
          ? errorMessage(mutation.error)
          : sessionFailed
            ? "기존 세션을 확인하지 못했습니다. 다시 로그인해 주세요."
            : "";
  const isRecovery = mode === "forgot" || mode === "reset";
  const isPortal = mode === "portal";
  const pending = mutation.isPending || resetMutation.isPending;
  const socialSignupPending = new URLSearchParams(window.location.hash.slice(1)).has("social-signup");
  const showForm = !isPortal && !socialSignupPending;

  return <main className="auth-scene" aria-labelledby="auth-title">
    <div className="auth-scene__shade" aria-hidden="true" />
    <div className="auth-stage">
      <header className="auth-ticket__title">
        <h1 id="auth-title" className="auth-visually-hidden">한짝 젓가락 키우기</h1>
        <img src={brandLogo} alt="" />
      </header>
      <section className={`auth-ticket auth-ticket--${mode}`}>
      <div className="auth-ticket__content">
        {isPortal && !socialSignupPending && <div className="auth-art-tabs auth-art-tabs--single">
          <span>로그인</span>
        </div>}
        {!isPortal && !isRecovery && !socialSignupPending && <div className="auth-art-tabs" role="tablist" aria-label="인증 방식">
          <button
            type="button"
            role="tab"
            className={mode === "login" ? "is-active" : ""}
            onClick={() => selectMode("login")}
            aria-selected={mode === "login"}
            aria-controls="auth-form-panel"
          >로그인</button>
          <button
            type="button"
            role="tab"
            className={mode === "signup" ? "is-active" : ""}
            onClick={() => selectMode("signup")}
            aria-selected={mode === "signup"}
            aria-controls="auth-form-panel"
          >회원가입</button>
        </div>}
        {isRecovery && <header className="auth-recovery-heading">
          <h2>{mode === "forgot" ? "비밀번호 찾기" : "비밀번호 재설정"}</h2>
          <p>{mode === "forgot" ? "가입할 때 사용한 이메일을 입력해 주세요." : "새 비밀번호를 입력해 주세요."}</p>
        </header>}

        {isPortal && !socialSignupPending && <div className="auth-portal">
          <SocialLoginPanel onAuthenticated={onAuthenticated} />
          <span className="auth-social__divider">또는</span>
          <button className="auth-portal__email" type="button" onClick={() => selectMode("login")}>이메일 로그인</button>
        </div>}

        {showForm && <div id="auth-form-panel" className={`auth-art-panel auth-art-panel--${mode}`} role="tabpanel">
          <form id="auth-form" className="auth-art-form" onSubmit={submit}>
            {mode !== "reset" && <label className="auth-art-field" htmlFor="auth-email">
              <span>이메일</span>
              <span className="auth-art-input">
                <img className="auth-field-icon" src={emailIcon} alt="" />
                <input
                  id="auth-email"
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="name@example.com"
                  required
                />
              </span>
            </label>}

            {mode === "signup" && <label className="auth-art-field" htmlFor="auth-nickname">
              <span>닉네임</span>
              <span className="auth-art-input auth-art-input--nickname">
                <img className="auth-field-icon auth-field-icon--nickname" src={userIcon} alt="" />
                <input
                  id="auth-nickname"
                  type="text"
                  autoComplete="nickname"
                  value={nickname}
                  onChange={(event) => setNickname(event.target.value)}
                  placeholder="함께할 이름"
                  maxLength={20}
                  required
                />
                <small className="auth-nickname-counter" aria-hidden="true">{nickname.length} / 20</small>
              </span>
            </label>}

            {mode !== "forgot" && <label className="auth-art-field" htmlFor="auth-password">
              <span>{mode === "reset" ? "새 비밀번호" : "비밀번호"}</span>
              <span className="auth-art-input auth-art-input--password">
                <img className="auth-field-icon" src={lockIcon} alt="" />
                <input
                  id="auth-password"
                  type={passwordVisible ? "text" : "password"}
                  autoComplete={mode === "login" ? "current-password" : "new-password"}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="8자 이상 입력"
                  minLength={8}
                  required
                />
                <button
                  className="auth-password-toggle"
                  type="button"
                  onClick={() => setPasswordVisible((visible) => !visible)}
                  aria-label={passwordVisible ? "비밀번호 숨기기" : "비밀번호 표시"}
                  aria-pressed={passwordVisible}
                >
                  <img src={passwordVisible ? passwordVisibleImage : passwordHiddenImage} alt="" />
                </button>
              </span>
            </label>}

            {mode === "reset" && <label className="auth-art-field" htmlFor="auth-password-confirmation">
              <span>비밀번호 확인</span>
              <span className="auth-art-input">
                <img className="auth-field-icon" src={lockIcon} alt="" />
                <input
                  id="auth-password-confirmation"
                  type={passwordVisible ? "text" : "password"}
                  autoComplete="new-password"
                  value={passwordConfirmation}
                  onChange={(event) => setPasswordConfirmation(event.target.value)}
                  minLength={8}
                  required
                />
              </span>
            </label>}

            {mode === "login" && <button className="auth-recovery-link" type="button" onClick={() => selectMode("forgot")}>비밀번호를 잊으셨나요?</button>}
            <p className={`auth-art-feedback${feedback ? " has-message" : ""}`} role={mutation.error || resetMutation.error || passwordsDiffer ? "alert" : "status"} aria-live="polite">{feedback}</p>
            <button
              className={`auth-art-submit auth-art-submit--${mode}`}
              type="submit"
              disabled={pending || (mode === "reset" && (password !== passwordConfirmation || resetMutation.isSuccess)) || resetMutation.isSuccess}
            >
              <span>{pending ? "처리 중" : mode === "login" ? "로그인" : mode === "signup" ? "계정 만들기" : mode === "forgot" ? "재설정 메일 보내기" : "비밀번호 변경"}</span>
            </button>
          </form>
        </div>}
        {isRecovery && <button className="auth-recovery-back" type="button" onClick={() => selectMode("login")}>로그인으로 돌아가기</button>}
        {showForm && !isRecovery && <button className="auth-portal-back" type="button" onClick={() => selectMode("portal")}>다른 방법으로 로그인</button>}
        {socialSignupPending && <SocialLoginPanel onAuthenticated={onAuthenticated} />}
      </div>

        <div className="auth-ticket__ornament" aria-hidden="true"><i /><i /><i /></div>
      </section>
    </div>
  </main>;
}
