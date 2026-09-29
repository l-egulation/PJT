import { useState, type FormEvent, type ReactNode } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { loadBrowserSettings, saveBrowserSettings, type BrowserSettings } from "./browserSettings";
import accentCoral from "../profile/assets-cozy-pixel-v2/accent-rays-coral-v2.png";
import accentYellow from "../profile/assets-cozy-pixel-v2/accent-rays-yellow-v2.png";
import sectionCheckIcon from "../profile/assets-cozy-pixel-v2/section-check-icon-32.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import { fileAsDataUrl, submitQaReport, type QaReportPayload } from "./qaReportApi";
import "./SettingsScreen.css";

export function SettingsScreen() {
  const queryClient = useQueryClient();
  const [settings, setSettings] = useState(loadBrowserSettings);
  const [reportOpen, setReportOpen] = useState(false);
  const [reportText, setReportText] = useState("");
  const [reportImage, setReportImage] = useState<string | undefined>();
  const [reportFileName, setReportFileName] = useState("");
  const [attachmentError, setAttachmentError] = useState("");
  const logout = useMutation({
    mutationFn: authApi.logout,
    onSuccess: (session) => {
      queryClient.setQueryData(["auth", "session"], session);
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "auth" });
    },
  });
  const deleteAccount = useMutation({
    mutationFn: authApi.deleteAccount,
    onSuccess: (session) => {
      queryClient.setQueryData(["auth", "session"], session);
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== "auth" });
    },
  });
  const report = useMutation({
    mutationFn: (payload: QaReportPayload) => submitQaReport(payload),
    onSuccess: () => {
      setReportText("");
      setReportImage(undefined);
      setReportFileName("");
    },
  });

  const changeSettings = (patch: Partial<BrowserSettings>) => {
    setSettings((current) => {
      const next = { ...current, ...patch };
      saveBrowserSettings(next);
      return next;
    });
  };

  return <main className="profile-settings-panel" aria-label="환경 및 계정 설정">
    <section className="profile-group settings-group">
      <SettingsHeading title="환경 설정" accent="coral" />
      <div className="settings-list">
        <SettingRow title="전체 소리">
          <Toggle checked={settings.soundEnabled} onChange={(checked) => changeSettings({ soundEnabled: checked })} label="전체 소리" />
        </SettingRow>
        <SettingRow title="볼륨">
          <div className={`settings-volume${settings.soundEnabled ? "" : " is-disabled"}`}>
            <input
              type="range"
              min="0"
              max="100"
              value={settings.volume}
              disabled={!settings.soundEnabled}
              onChange={(event) => changeSettings({ volume: Number(event.target.value) })}
              aria-label="게임 볼륨"
            />
            <output>{settings.volume}%</output>
          </div>
        </SettingRow>
        <SettingRow title="절전 모드">
          <Toggle checked={settings.powerSavingEnabled} onChange={(checked) => changeSettings({ powerSavingEnabled: checked })} label="절전 모드" />
        </SettingRow>
        <SettingRow title="버그 신고">
          <button type="button" className="settings-report-open" onClick={() => { report.reset(); setReportOpen(true); }}>QA 신고하기</button>
        </SettingRow>
      </div>
    </section>

    <section className="profile-group settings-group settings-account-group">
      <SettingsHeading title="계정" accent="yellow" />
      <div className="settings-list">
        <SettingRow title="로그아웃">
          <button type="button" className="settings-logout" disabled={logout.isPending} onClick={() => logout.mutate()}>
            {logout.isPending ? "로그아웃 중…" : "로그아웃"}
          </button>
        </SettingRow>
        <SettingRow title="계정 탈퇴">
          <button
            type="button"
            className="settings-delete"
            disabled={deleteAccount.isPending}
            onClick={() => {
              if (window.confirm("계정을 탈퇴하면 현재 계정이 익명화되고 즉시 로그아웃됩니다. 계속할까요?")) deleteAccount.mutate();
            }}
          >{deleteAccount.isPending ? "탈퇴 처리 중…" : "계정 탈퇴"}</button>
        </SettingRow>
      </div>
    </section>
    {logout.error && <p className="settings-feedback" role="alert">로그아웃하지 못했습니다. 다시 시도해 주세요.</p>}
    {deleteAccount.error && <p className="settings-feedback" role="alert">계정 탈퇴에 실패했습니다. 다시 시도해 주세요.</p>}
    {reportOpen && <div className="qa-report-backdrop" onMouseDown={(event) => event.target === event.currentTarget && !report.isPending && setReportOpen(false)}>
      <form className="qa-report-dialog" role="dialog" aria-modal="true" aria-labelledby="qa-report-title" onSubmit={(event: FormEvent) => {
        event.preventDefault();
        if (!reportText.trim() || report.isPending) return;
        report.mutate({
          description: reportText.trim(),
          pageUrl: window.location.href,
          userAgent: navigator.userAgent,
          viewportWidth: window.innerWidth,
          viewportHeight: window.innerHeight,
          imageDataUrl: reportImage,
        });
      }}>
        <header><div><small>발견한 문제를 알려주세요</small><h3 id="qa-report-title">QA 버그 신고</h3></div><button type="button" aria-label="신고 창 닫기" disabled={report.isPending} onClick={() => setReportOpen(false)}><img src={closeIcon} alt="" /></button></header>
        {report.isSuccess ? <div className="qa-report-success" role="status"><strong>신고가 접수됐어요.</strong><span>화면 정보와 함께 개발팀에 저장했습니다.</span><button type="button" onClick={() => setReportOpen(false)}>확인</button></div> : <>
          <label className="qa-report-description"><span>어떤 문제가 있었나요?</span><textarea value={reportText} maxLength={2000} required autoFocus placeholder="재현 순서와 기대했던 동작을 적어주세요." onChange={(event) => setReportText(event.target.value)} /><small>{reportText.length.toLocaleString()} / 2,000</small></label>
          <label className="qa-report-image"><span>사진 첨부 <small>선택 · PNG/JPG/WebP · 최대 4MB</small></span><input type="file" accept="image/png,image/jpeg,image/webp" onChange={async (event) => {
            const file = event.currentTarget.files?.[0];
            setAttachmentError("");
            if (!file) { setReportImage(undefined); setReportFileName(""); return; }
            try { setReportImage(await fileAsDataUrl(file)); setReportFileName(file.name); }
            catch (error) { setReportImage(undefined); setReportFileName(""); setAttachmentError(error instanceof Error && error.message === "IMAGE_TOO_LARGE" ? "사진은 4MB 이하만 첨부할 수 있어요." : "PNG, JPG, WebP 사진만 첨부할 수 있어요."); }
          }} />{reportFileName && <b>{reportFileName}</b>}</label>
          {attachmentError && <p className="qa-report-error" role="alert">{attachmentError}</p>}
          {report.isError && <p className="qa-report-error" role="alert">신고를 보내지 못했어요. 잠시 뒤 다시 시도해 주세요.</p>}
          <p className="qa-report-context">현재 화면 주소와 {window.innerWidth}×{window.innerHeight} 환경이 자동으로 함께 저장됩니다.</p>
          <button className="qa-report-submit" type="submit" disabled={!reportText.trim() || report.isPending}>{report.isPending ? "접수 중…" : "신고 접수"}</button>
        </>}
      </form>
    </div>}
  </main>;
}

function SettingsHeading({ title, accent }: { title: string; accent: "coral" | "yellow" }) {
  return <div className="profile-group-heading">
    <img src={sectionCheckIcon} alt="" />
    <h3>{title}</h3>
    <img className="profile-heading-accent" src={accent === "coral" ? accentCoral : accentYellow} alt="" />
    <i aria-hidden="true" />
  </div>;
}

function SettingRow({ title, children }: { title: string; children: ReactNode }) {
  return <div className="settings-row">
    <strong>{title}</strong>
    {children}
  </div>;
}

function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (checked: boolean) => void; label: string }) {
  return <button
    type="button"
    className={`settings-toggle${checked ? " is-on" : ""}`}
    role="switch"
    aria-checked={checked}
    aria-label={label}
    onClick={() => onChange(!checked)}
  >
    <span aria-hidden="true" />
    <strong aria-hidden="true">{checked ? "켜짐" : "꺼짐"}</strong>
  </button>;
}
