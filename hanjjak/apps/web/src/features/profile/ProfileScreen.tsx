import { useEffect, useRef, useState, type FormEvent, type ReactNode, type RefObject } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { useBattleRuntimeStore } from "../battle/runtimeStore";
import { gameSessionClient } from "../runtime/gameSessionClient";
import { runtimeCheckpoints } from "../runtime/checkpoints";
import { SettingsScreen } from "../settings/SettingsScreen";
import { SocialConnections } from "./SocialConnections";
import accentCoral from "./assets-cozy-pixel-v2/accent-rays-coral-v2.png";
import accentYellow from "./assets-cozy-pixel-v2/accent-rays-yellow-v2.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import editPencilIcon from "./assets-cozy-pixel-v2/edit-pencil-icon.png";
import sectionCheckIcon from "./assets-cozy-pixel-v2/section-check-icon-32.png";
import sproutIcon from "./assets-cozy-pixel-v2/sprout-icon-32.png";
import "./ProfileScreen.css";

const PROFILE_ERRORS: Record<string, string> = {
  AUTHENTICATION_REQUIRED: "다시 로그인해 주세요.",
  NICKNAME_REQUIRED: "닉네임을 입력해 주세요.",
  NICKNAME_TOO_LONG: "닉네임은 20자 이하로 입력해 주세요.",
  CURRENT_PASSWORD_REQUIRED: "현재 비밀번호를 입력해 주세요.",
  CURRENT_PASSWORD_INVALID: "현재 비밀번호가 올바르지 않습니다.",
  PASSWORD_TOO_SHORT: "새 비밀번호는 8자 이상이어야 합니다.",
  PASSWORD_CONFIRMATION_REQUIRED: "새 비밀번호 확인을 입력해 주세요.",
  PASSWORD_CONFIRMATION_MISMATCH: "새 비밀번호와 확인이 일치하지 않습니다.",
  IDEMPOTENCY_KEY_REUSED: "요청이 충돌했습니다. 다시 시도해 주세요.",
};

function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message in PROFILE_ERRORS) return PROFILE_ERRORS[error.message];
  return "프로필을 저장하지 못했습니다. 잠시 후 다시 시도해 주세요.";
}

type ProfileScreenProps = {
  open: boolean;
  triggerRef?: RefObject<HTMLButtonElement | null>;
  initialTab?: ProfileTab;
  /* 튜토리얼 미리보기는 모달 대신 화면 안에 그대로 얹는다. showModal 이 만든
     최상위 레이어 위에는 안내 말풍선을 올릴 수 없기 때문이다. */
  inline?: boolean;
  onClose: () => void;
};

export type ProfileTab = "profile" | "social" | "settings";

const PROFILE_TAB_TITLES: Record<ProfileTab, string> = {
  profile: "내 정보",
  social: "소셜",
  settings: "설정",
};

type ProfileFieldProps = {
  label: string;
  value: string;
  icon?: "person" | "mail" | "sprout" | "star" | "rice";
  tone?: "coral" | "cyan";
};

export function ProfileScreen({ open, triggerRef, initialTab = "profile", inline = false, onClose }: ProfileScreenProps) {
  const queryClient = useQueryClient();
  const resetBattleRuntime = useBattleRuntimeStore((state) => state.reset);
  const dialog = useRef<HTMLDialogElement>(null);
  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, enabled: open, retry: false });
  const account = session.data?.account ?? null;
  const [nickname, setNickname] = useState(account?.nickname ?? "");
  const [editingNickname, setEditingNickname] = useState(false);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordValidationError, setPasswordValidationError] = useState("");
  const [activeTab, setActiveTab] = useState<ProfileTab>(initialTab);

  useEffect(() => {
    if (!open || inline) return;
    const previous = triggerRef?.current ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    const element = dialog.current;
    element?.showModal();
    element?.focus();
    return () => {
      element?.close();
      previous?.focus();
    };
  }, [inline, open, triggerRef]);

  useEffect(() => {
    if (account) setNickname(account.nickname);
  }, [account]);

  useEffect(() => {
    if (open) setActiveTab(initialTab);
  }, [initialTab, open]);

  const updateNickname = useMutation({
    mutationFn: authApi.updateNickname,
    onSuccess: (updated) => {
      queryClient.setQueryData(["auth", "session"], { authenticated: true, account: updated });
      setEditingNickname(false);
    },
  });

  const changePassword = useMutation({
    mutationFn: authApi.changePassword,
    onSuccess: async (nextSession) => {
      gameSessionClient.invalidate();
      resetBattleRuntime();
      await runtimeCheckpoints.clearRuntime(account?.accountId ?? "").catch(() => undefined);
      queryClient.setQueryData(["auth", "session"], nextSession);
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "auth" });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setPasswordValidationError("");
    },
  });

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    updateNickname.mutate({ nickname: nickname.trim() });
  };

  const trimmedNickname = nickname.trim();

  const submitPassword = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    changePassword.reset();
    if (newPassword !== confirmPassword) {
      setPasswordValidationError("새 비밀번호가 서로 일치하지 않습니다.");
      return;
    }
    setPasswordValidationError("");
    changePassword.mutate({ currentPassword, newPassword, newPasswordConfirmation: confirmPassword });
  };

  const resetPasswordFeedback = () => {
    changePassword.reset();
    setPasswordValidationError("");
  };

  const paper = open ? <div className="profile-paper">
        <aside className="profile-sidebar" aria-label="마이페이지 메뉴">
          <div className="profile-sidebar-title">
            <img src={sproutIcon} alt="" />
            <strong>마이페이지</strong>
          </div>
          <nav>
            <button
              type="button"
              className={`profile-tab-red${activeTab === "profile" ? " is-active" : ""}`}
              aria-current={activeTab === "profile" ? "page" : undefined}
              onClick={() => setActiveTab("profile")}
            >내 정보</button>
            <button
              type="button"
              className={`profile-tab-blue${activeTab === "social" ? " is-active" : ""}`}
              aria-current={activeTab === "social" ? "page" : undefined}
              onClick={() => setActiveTab("social")}
            >소셜</button>
            <button
              type="button"
              className={`profile-tab-red${activeTab === "settings" ? " is-active" : ""}`}
              aria-current={activeTab === "settings" ? "page" : undefined}
              onClick={() => setActiveTab("settings")}
            >설정</button>
          </nav>
        </aside>

        <section className="profile-content">
          <header className="profile-header">
            <div className="profile-brand">
              <h2 id="profile-title">{PROFILE_TAB_TITLES[activeTab]}</h2>
            </div>

            <button type="button" className="profile-close" onClick={onClose} aria-label="마이페이지 닫기">
              <img src={closeIcon} alt="" />
            </button>
          </header>

          {session.isLoading ? <ProfileMessage>내 정보를 불러오는 중입니다.</ProfileMessage> : !account ? <ProfileMessage error>로그인이 필요합니다.</ProfileMessage> : <div className="profile-body">
          {activeTab === "settings" ? <SettingsScreen /> : activeTab === "social" ? <main className="profile-social" aria-label="소셜 로그인 연결">
            <SectionHeading title="소셜 로그인 연결" accent="coral" />
            <SocialConnections />
          </main> : <main className="profile-record">
            <article className="profile-group profile-account-group">
              <SectionHeading title="계정 정보" accent="coral">
                <details className="profile-technical">
                  <summary>식별 정보<span aria-hidden="true" /></summary>
                  <dl>
                    <ProfileField label="계정 ID" value={account.accountId} icon="person" />
                    <ProfileField label="캐릭터 ID" value={account.characterId} icon="sprout" />
                  </dl>
                </details>
              </SectionHeading>
              <dl className="profile-field-grid profile-account-rows">
                <div className="profile-nickname-row">
                  <dt><span className="profile-stat-icon person" aria-hidden="true" />닉네임</dt>
                  <dd>
                    {editingNickname ? <form className="profile-inline-nickname-form" onSubmit={submit}>
                      <div className="profile-input-wrap">
                        <input
                          id="profile-nickname"
                          aria-label="새 닉네임"
                          value={nickname}
                          onChange={(event) => {
                            updateNickname.reset();
                            setNickname(event.target.value);
                          }}
                          maxLength={20}
                          required
                          autoFocus
                          aria-describedby="profile-nickname-count"
                        />
                        <span id="profile-nickname-count">{nickname.length} / 20</span>
                      </div>
                      <button type="submit" disabled={updateNickname.isPending || !trimmedNickname || trimmedNickname === account.nickname}>
                        {updateNickname.isPending ? "저장 중…" : "저장"}
                      </button>
                      <button
                        type="button"
                        className="profile-nickname-cancel"
                        onClick={() => {
                          updateNickname.reset();
                          setNickname(account.nickname);
                          setEditingNickname(false);
                        }}
                      >취소</button>
                    </form> : <div className="profile-nickname-display">
                      <span>{account.nickname}</span>
                      <button
                        type="button"
                        className="profile-nickname-edit"
                        aria-label="닉네임 수정"
                        onClick={() => {
                          updateNickname.reset();
                          setNickname(account.nickname);
                          setEditingNickname(true);
                        }}
                      ><img src={editPencilIcon} alt="" /></button>
                    </div>}
                  </dd>
                </div>
                <ProfileField label="이메일" value={account.email} icon="mail" />
              </dl>
              {updateNickname.error && <p className="profile-feedback error" role="alert">{errorMessage(updateNickname.error)}</p>}
              {updateNickname.isSuccess && !editingNickname && <p className="profile-feedback success" role="status">닉네임을 저장했습니다.</p>}
            </article>

            <article className="profile-group profile-status-group">
              <SectionHeading title="캐릭터 상태" accent="yellow" />
              <dl className="profile-field-grid profile-status-grid" aria-label="캐릭터 상태">
                <ProfileField label="레벨" value={`Lv. ${account.level.toLocaleString()}`} icon="sprout" tone="coral" />
                <ProfileField label="누적 EXP" value={account.experience.toLocaleString()} icon="star" tone="cyan" />
                <ProfileField label="보유 쌀" value={account.rice.toLocaleString()} icon="rice" />
              </dl>
            </article>

            <article className="profile-group profile-password-group">
              <SectionHeading title="비밀번호 변경" accent="yellow">
                <span className="profile-password-status" id="profile-password-status" aria-live="polite">
                  {changePassword.isPending ? "변경 중…" : changePassword.isSuccess ? "변경 완료" : "변경 가능"}
                </span>
              </SectionHeading>
              <form className="profile-password-form" onSubmit={submitPassword} aria-describedby="profile-password-status">
                <label htmlFor="profile-current-password">
                  현재 비밀번호
                  <input
                    id="profile-current-password"
                    type="password"
                    autoComplete="current-password"
                    value={currentPassword}
                    onChange={(event) => { resetPasswordFeedback(); setCurrentPassword(event.target.value); }}
                    required
                  />
                </label>
                <label htmlFor="profile-new-password">
                  새 비밀번호
                  <input
                    id="profile-new-password"
                    type="password"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(event) => { resetPasswordFeedback(); setNewPassword(event.target.value); }}
                    minLength={8}
                    required
                  />
                </label>
                <label htmlFor="profile-confirm-password">
                  새 비밀번호 확인
                  <input
                    id="profile-confirm-password"
                    type="password"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(event) => { resetPasswordFeedback(); setConfirmPassword(event.target.value); }}
                    aria-invalid={Boolean(passwordValidationError)}
                    minLength={8}
                    required
                  />
                </label>
                <button
                  type="submit"
                  disabled={changePassword.isPending || !currentPassword || newPassword.length < 8 || confirmPassword.length < 8}
                >{changePassword.isPending ? "변경 중…" : "비밀번호 변경"}</button>
              </form>
              {(passwordValidationError || changePassword.error) && <p className="profile-password-feedback error" role="alert">
                {passwordValidationError || errorMessage(changePassword.error)}
              </p>}
            </article>
          </main>}
          </div>}
        </section>
      </div> : null;

  if (inline) return <div className={`profile-window profile-window--${activeTab}`}>{paper}</div>;

  return (
    <dialog
      ref={dialog}
      className={`profile-window profile-window--${activeTab}`}
      tabIndex={-1}
      aria-labelledby="profile-title"
      onCancel={(event) => { event.preventDefault(); onClose(); }}
    >
      {paper}
    </dialog>
  );
}

function SectionHeading({ title, accent, children }: { title: string; accent: "coral" | "yellow"; children?: ReactNode }) {
  return <div className="profile-group-heading">
    <img src={sectionCheckIcon} alt="" />
    <h3>{title}</h3>
    <img className="profile-heading-accent" src={accent === "coral" ? accentCoral : accentYellow} alt="" />
    <i aria-hidden="true" />
    {children}
  </div>;
}

function ProfileField({ label, value, icon = "person", tone }: ProfileFieldProps) {
  return <div className={tone ? `tone-${tone}` : undefined}>
    <dt><span className={`profile-stat-icon ${icon}`} aria-hidden="true" />{label}</dt>
    <dd>{value}</dd>
  </div>;
}

function ProfileMessage({ children, error = false }: { children: string; error?: boolean }) {
  return <div className={`profile-message${error ? " error" : ""}`} role={error ? "alert" : "status"}>{children}</div>;
}
