import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../auth/api";

export function SocialConnections() {
  const queryClient = useQueryClient();
  const connections = useQuery({ queryKey: ["auth", "social-connections"], queryFn: authApi.socialConnections, retry: false });
  const providers = useQuery({ queryKey: ["auth", "social-providers"], queryFn: authApi.socialProviders, retry: false });
  const begin = useMutation({
    mutationFn: (provider: string) => authApi.beginSocial(provider, true),
    onSuccess: ({ authorizationUrl }) => window.location.assign(authorizationUrl),
  });
  const unlink = useMutation({
    mutationFn: authApi.unlinkSocial,
    onSuccess: (updated) => queryClient.setQueryData(["auth", "social-connections"], updated),
  });

  if (connections.isLoading || providers.isLoading) return <p className="profile-form-note">로그인 수단을 확인하는 중입니다.</p>;
  if (!connections.data) return <p className="profile-feedback error" role="alert">로그인 수단을 불러오지 못했습니다.</p>;

  return <div className="profile-social-connections">
    {providers.data?.map((provider) => {
      const connection = connections.data.providers.find((item) => item.provider === provider.id);
      const canUnlink = connections.data!.passwordEnabled || connections.data!.providers.length > 1;
      return <div key={provider.id}>
        <span><strong>{provider.displayName}</strong>{connection?.email && <small>{connection.email}</small>}</span>
        {connection
          ? <button type="button" disabled={!canUnlink || unlink.isPending} title={!canUnlink ? "마지막 로그인 수단은 해제할 수 없습니다." : undefined} onClick={() => unlink.mutate(provider.id)}>연결 해제</button>
          : <button type="button" disabled={!provider.enabled || begin.isPending} onClick={() => begin.mutate(provider.id)}>{provider.enabled ? "연결" : "미설정"}</button>}
      </div>;
    })}
    {(begin.error || unlink.error) && <p className="profile-feedback error" role="alert">로그인 수단을 변경하지 못했습니다.</p>}
    {!connections.data.passwordEnabled && <p className="profile-form-note">비밀번호가 없는 계정입니다. 마지막 소셜 로그인 수단은 보호됩니다.</p>}
  </div>;
}
