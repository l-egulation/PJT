import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { authApi, type AccountIdentity, type SocialProvider } from "./api";
import googleButton from "./assets-social/google-button.png";
import kakaoButton from "./assets-social/kakao-button.png";
import ssafyGitlabButton from "./assets-social/ssafy-gitlab-button.png";

/** 공급자 id는 서버가 정하므로 부분 일치로 팻말 에셋을 고른다. */
const PROVIDER_SHELLS: { match: RegExp; image: string; modifier: string }[] = [
  { match: /google/i, image: googleButton, modifier: "google" },
  { match: /kakao/i, image: kakaoButton, modifier: "kakao" },
  { match: /gitlab/i, image: ssafyGitlabButton, modifier: "gitlab" },
];

function shellFor(provider: SocialProvider) {
  return PROVIDER_SHELLS.find((shell) => shell.match.test(provider.id) || shell.match.test(provider.displayName));
}

const SOCIAL_ERRORS: Record<string, string> = {
  SOCIAL_PROVIDER_DISABLED: "현재 이 로그인 방식은 사용할 수 없습니다.",
  SOCIAL_STATE_INVALID: "로그인 요청이 만료되었거나 올바르지 않습니다.",
  SOCIAL_LOGIN_EXPIRED: "로그인 시간이 만료되었습니다. 다시 시도해 주세요.",
  SOCIAL_AUTHORIZATION_DENIED: "소셜 로그인이 취소되었습니다.",
  SOCIAL_ID_TOKEN_INVALID: "공급자 인증 정보를 확인하지 못했습니다.",
  SOCIAL_NONCE_INVALID: "로그인 요청 검증에 실패했습니다.",
  SOCIAL_PROVIDER_RESPONSE_INVALID: "로그인 공급자 응답을 확인하지 못했습니다.",
};

function socialErrorFromHash(): string {
  const code = new URLSearchParams(window.location.hash.slice(1)).get("social-error");
  if (!code) return "";
  return SOCIAL_ERRORS[code] ?? "소셜 로그인을 완료하지 못했습니다.";
}

export function SocialLoginPanel({ onAuthenticated }: { onAuthenticated: (account: AccountIdentity) => void }) {
  const signupToken = new URLSearchParams(window.location.hash.slice(1)).get("social-signup");
  const providers = useQuery({ queryKey: ["auth", "social-providers"], queryFn: authApi.socialProviders, retry: false });
  const pending = useQuery({ queryKey: ["auth", "social-pending"], queryFn: authApi.pendingSocialSignup, enabled: Boolean(signupToken), retry: false });
  const begin = useMutation({
    mutationFn: authApi.beginSocial,
    onSuccess: ({ authorizationUrl }) => window.location.assign(authorizationUrl),
  });

  if (signupToken) return <SocialNicknameSignup token={signupToken} pending={pending.data} onAuthenticated={onAuthenticated} />;
  const enabled = providers.data?.filter((provider) => provider.enabled) ?? [];
  const callbackError = socialErrorFromHash();
  if (enabled.length === 0 && !providers.isLoading) return callbackError ? <p className="auth-social-error" role="alert">{callbackError}</p> : null;

  return <section className="auth-social auth-social--portal" aria-label="소셜 로그인">
    {enabled.map((provider) => {
      const shell = shellFor(provider);
      return <button
        key={provider.id}
        type="button"
        className={`auth-social__shell${shell ? ` auth-social__shell--${shell.modifier}` : ""}`}
        style={shell ? { backgroundImage: `url(${shell.image})` } : undefined}
        disabled={begin.isPending}
        onClick={() => begin.mutate(provider.id)}
      >{provider.displayName} 로그인</button>;
    })}
    {(begin.error || callbackError) && <p className="auth-social-error" role="alert">{callbackError || "소셜 로그인을 시작하지 못했습니다."}</p>}
  </section>;
}

function SocialNicknameSignup({ token, pending, onAuthenticated }: {
  token: string;
  pending?: { suggestedNickname: string | null; provider: string; email: string | null };
  onAuthenticated: (account: AccountIdentity) => void;
}) {
  const [nickname, setNickname] = useState("");
  useEffect(() => {
    if (pending?.suggestedNickname) setNickname(pending.suggestedNickname);
  }, [pending?.suggestedNickname]);
  const signup = useMutation({
    mutationFn: () => authApi.finishSocialSignup({ token, nickname: nickname.trim() }),
    onSuccess: (account) => {
      window.history.replaceState({}, "", window.location.pathname);
      onAuthenticated(account);
    },
  });
  return <section className="auth-social-signup" aria-labelledby="social-signup-title">
    <h2 id="social-signup-title">닉네임을 정해 주세요</h2>
    <p>닉네임을 확정한 뒤 계정을 만듭니다. 같은 이메일의 기존 계정과 자동으로 합치지 않습니다.</p>
    <form onSubmit={(event) => { event.preventDefault(); signup.mutate(); }}>
      <label htmlFor="social-nickname">닉네임</label>
      <input id="social-nickname" value={nickname} maxLength={20} required onChange={(event) => setNickname(event.target.value)} />
      <button type="submit" disabled={signup.isPending || !nickname.trim()}>{signup.isPending ? "계정 생성 중" : "계정 만들기"}</button>
    </form>
    {signup.error && <p className="auth-social-error" role="alert">계정을 만들지 못했습니다. 다시 시도해 주세요.</p>}
  </section>;
}
