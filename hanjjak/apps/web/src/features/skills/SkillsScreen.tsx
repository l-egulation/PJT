import { useEffect, useMemo, useRef, useState } from "react";
import { layout, prepare, type PreparedText } from "@chenglou/pretext";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import riceIcon from "../equipment/assets-cozy-pixel/currency-rice-gold-256.png";
import skillbookIcon from "../../shared/assets/cozy-hud-v1/icons/reward-skillbook.png";
import skillsTitleIcon from "../../shared/assets/cozy-hud-v1/icons/bottom-skills.png";
import closeIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import { isUncertainSkillCommandError, skillsApi, type SkillActionSummary, type SkillCommand, type SkillState, type SkillSummary } from "./api";
import { TopAlert } from "../../shared/TopAlert";
import { NoticeToast } from "../../shared/NoticeToast";

type SkillLoadoutCommand = { ids: string[]; key: string };
type SkillNotice = { id: number; duration?: number; title: string; message: string; detail?: string; tone?: "success" | "failure"; retry?: "command" | "loadout" };
type SkillCardCommand = (kind: "enhance" | "promote", id: string) => void;
/** 단추에 그대로 붙일 수 없는 말(잠금·완료)까지 "하기"를 붙이지 않는다. */
const ACTION_BUTTON_SUFFIXED = new Set(["강화", "승급", "해금"]);

const SKILL_ART_FILES: Record<string, string> = {
  active_heavy: "skill-active-heavy.png",
  active_dot: "skill-active-dot.png",
  active_haste: "skill-active-haste.png",
  active_basic_amp: "skill-active-basic-amp.png",
  passive_critical: "skill-passive-critical.png",
  passive_all_damage: "skill-passive-all-damage.png",
};
const skillArtModules = import.meta.glob("./assets-cozy-pixel/skill-*.png", { eager: true, query: "?url", import: "default" }) as Record<string, string>;
const ACTION_LABELS: Record<string, string> = { UNLOCK: "해금", ENHANCE: "강화", PROMOTE: "승급", LOCKED: "잠금", COMPLETE: "최대" };
const ERROR_LABELS: Record<string, string> = { INSUFFICIENT_SKILLBOOK: "스킬북 부족", INSUFFICIENT_RICE: "쌀 부족", SKILL_GRADE_LOCKED: "전설 승급 잠금", SKILL_MAX_LEVEL: "현재 등급 최대 강화", SKILL_MAX_GRADE: "최종 등급 도달" };

function rate(bp: number) {
  return `${(bp / 100).toFixed(bp % 100 === 0 ? 0 : 1)}%`;
}

function gradeClass(grade: string | null) {
  return grade ? grade.toLowerCase() : "locked";
}

function gradeName(grade: string | null) {
  if (grade === "RARE") return "희귀";
  if (grade === "EPIC") return "영웅";
  if (grade === "LEGENDARY") return "전설";
  return "노말";
}

/** 카드에 붙일 한마디. 지금 올릴 수 있으면 무엇으로 올라가는지, 아니면 왜 못 하는지. */
export function skillReadiness(skill: SkillSummary): { label: string; tone: "ready" | "short" | "done" } {
  if (skill.action.kind === "COMPLETE") return { label: "최대", tone: "done" };
  if (skill.action.executable) return { label: `${ACTION_LABELS[skill.action.kind] ?? "강화"} 가능`, tone: "ready" };
  const reason = skill.action.disabledReason;
  return { label: reason ? ERROR_LABELS[reason] ?? reason : "조건 미달", tone: "short" };
}

function actionCopy(skill: SkillSummary) {
  const visible = ACTION_LABELS[skill.action.kind] ?? "강화";
  if (!skill.action.targetGrade || !skill.action.targetLevel) return { visible, accessible: visible };
  const current = skill.gradeName ?? "미해금";
  return { visible, accessible: `${current} → ${gradeName(skill.action.targetGrade)} +${skill.action.targetLevel} ${visible}` };
}

function usePretextCardLayout(contentKey: string) {
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!document.fonts || typeof ResizeObserver === "undefined") return;
    let disposed = false;
    let observer: ResizeObserver | null = null;
    void document.fonts.ready.then(() => {
      if (disposed || !root.current) return;
      const prepared = new Map<HTMLElement, PreparedText>();
      root.current.querySelectorAll<HTMLElement>("[data-pretext]").forEach(element => prepared.set(element, prepare(element.textContent ?? "", getComputedStyle(element).font, { wordBreak: "keep-all" })));
      const relayout = () => prepared.forEach((text, element) => {
        const lineHeight = Number.parseFloat(getComputedStyle(element).lineHeight) || 18;
        element.style.minHeight = `${Math.ceil(layout(text, Math.max(element.clientWidth, 1), lineHeight).height)}px`;
      });
      observer = new ResizeObserver(relayout);
      observer.observe(root.current);
      relayout();
    });
    return () => { disposed = true; observer?.disconnect(); };
  }, [contentKey]);
  return root;
}

export function SkillArtwork({ skillId, name, compact = false }: { skillId: string; name: string; compact?: boolean }) {
  const file = SKILL_ART_FILES[skillId];
  const source = file ? skillArtModules[`./assets-cozy-pixel/${file}`] : undefined;
  return source
    ? <img className={`skill-artwork ${compact ? "compact" : ""}`} src={source} alt={`${name} 스킬 아이콘`} />
    : <span className={`skill-artwork skill-artwork-missing ${compact ? "compact" : ""}`} role="img" aria-label={`${name} 스킬 아이콘 준비 중`}><b>?</b><small>에셋 준비 중</small></span>;
}

export function SkillGradeBadge({ skill }: { skill: SkillSummary }) {
  return <span className={`skill-grade skill-grade-${gradeClass(skill.grade)}`}>{skill.gradeName ?? "미해금"}</span>;
}

function BookCost({ action, actionText }: { action: SkillActionSummary; actionText: string }) {
  if (action.books.length === 0) return <span className="skill-resource muted"><img src={skillbookIcon} alt="" aria-hidden="true" /><strong>0</strong></span>;
  return <div className="skill-book-cost" aria-label={`${actionText}에 필요한 등급별 스킬북`}><img src={skillbookIcon} alt="" aria-hidden="true" /><div>{action.books.map(book => {
    const grade = book.itemId.split(":").at(-1)?.toUpperCase() ?? book.displayName;
    const short = book.availableQuantity < book.requiredQuantity;
    return <span key={book.itemId} className={short ? "short" : ""} title={book.displayName}><b>{gradeName(grade)}</b><strong>{book.availableQuantity.toLocaleString()} / {book.requiredQuantity.toLocaleString()}</strong></span>;
  })}</div></div>;
}

export function SkillResourceCost({ skill, riceBalance }: { skill: SkillSummary; riceBalance: number }) {
  const actionText = ACTION_LABELS[skill.action.kind] ?? "강화";
  return <div className="skill-resource-cost" aria-label="필요 재료">
    <BookCost action={skill.action} actionText={actionText} />
    <span className={`skill-resource ${riceBalance < skill.action.riceCost ? "short" : ""}`}><img src={riceIcon} alt="" aria-hidden="true" /><strong>{skill.action.riceCost.toLocaleString()}</strong></span>
  </div>;
}

export function SkillLoadout({ skills, activeLoadout, selectingSlot, busy, onSlotClick }: { skills: SkillSummary[]; activeLoadout: string[]; selectingSlot: number | null; busy: boolean; onSlotClick: (index: number, skill?: SkillSummary) => void }) {
  return <section className="skill-loadout" aria-labelledby="skill-loadout-title"><h3 id="skill-loadout-title">자동 사용</h3><ol>{Array.from({ length: 4 }, (_, index) => {
    const skillId = activeLoadout[index];
    const skill = skills.find(candidate => candidate.skillId === skillId);
    const disabled = busy || (!skill && index !== activeLoadout.length);
    const label = skill ? `${index + 1}번 슬롯의 ${skill.name} 장착 해제` : `${index + 1}번 슬롯에 스킬 장착`;
    const activate = () => { if (!disabled) onSlotClick(index, skill); };
    return <li key={index} role="button" aria-label={label} aria-disabled={disabled} aria-pressed={!skill && selectingSlot === index} tabIndex={disabled ? -1 : 0} className={`${skill ? "filled" : "empty"} ${selectingSlot === index ? "selecting" : ""}`} onClick={activate} onKeyDown={event => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); activate(); } }}><b>{index + 1}</b>{skill ? <SkillArtwork skillId={skill.skillId} name={skill.name} compact /> : <i>+</i>}<span className="sr-only">{skill ? skill.name : selectingSlot === index ? "스킬 선택 중" : "비어 있음"}</span></li>;
  })}</ol></section>;
}

/*
 * 카드는 "무엇인가"만 말한다. 여섯 장이 나란히 서는 자리라 값과 단추까지 넣으면
 * 한 장이 읽히지 않는다. 값·성공률·강화 단추는 고른 스킬 하나에 대해서만 아래 칸이 맡는다.
 */
export function SkillCard({ skill, equippedSlot, busy, selecting, picked, onPick, onSelect }: { skill: SkillSummary; equippedSlot: number | null; busy: boolean; selecting: boolean; picked: boolean; onPick: (skill: SkillSummary) => void; onSelect: (skill: SkillSummary) => void }) {
  const equipped = equippedSlot !== null;
  const selectable = selecting && skill.active && skill.unlocked && !equipped && !busy;
  const selectionBlocked = selecting && !selectable ? equipped ? "장착 중" : skill.active ? "해금 필요" : "자동 사용 불가" : null;
  const readiness = skillReadiness(skill);
  return <article className={`skill-growth-card ${skill.unlocked ? "unlocked" : "locked"} ${selectable ? "selectable" : ""} ${picked ? "picked" : ""}`}>
    {selectable && <button type="button" className="skill-card-select-hitbox" aria-label={`${skill.name} 자동 사용 장착`} onClick={() => onSelect(skill)} />}
    {!selecting && <button type="button" className="skill-card-pick" aria-pressed={picked} aria-label={`${skill.name} 강화 대상으로 고르기`} onClick={() => onPick(skill)} />}
    {selectionBlocked && <span className="skill-selection-blocked" aria-label={selectionBlocked}><i aria-hidden="true" /><b>{selectionBlocked}</b></span>}
    <span className={`skill-kind ${skill.active ? "is-active" : "is-passive"}`}>{skill.active ? "액티브" : "패시브"}</span>
    <div className="skill-card-art"><SkillArtwork skillId={skill.skillId} name={skill.name} />{equipped && <mark>AUTO {equippedSlot + 1}</mark>}</div>
    <div className="skill-card-copy"><h3 data-pretext>{skill.name}</h3><div className="skill-card-meta"><SkillGradeBadge skill={skill} />{skill.unlocked && <strong>+{skill.level}강</strong>}{/* 모자랄 때는 조용히 둔다. 카드마다 빨간 딱지가 붙으면 올릴 수 있는 것이 묻힌다. */}
      {readiness.tone !== "short" && <span className={`skill-readiness is-${readiness.tone}`}><i aria-hidden="true" />{readiness.label}</span>}</div></div>
    <p className="skill-effect" data-pretext>{skill.effectText}</p>
  </article>;
}

/** 고른 스킬 하나의 값·성공률·강화 단추. 카드 여섯 장 아래에 한 칸으로 둔다. */
export function SkillEnhancePanel({ skill, riceBalance, busy, blocked, onCommand }: { skill: SkillSummary; riceBalance: number; busy: boolean; blocked: boolean; onCommand: SkillCardCommand }) {
  const { visible, accessible } = actionCopy(skill);
  const commandKind = skill.action.kind === "PROMOTE" ? "promote" : "enhance";
  const reason = skill.action.disabledReason ? ERROR_LABELS[skill.action.disabledReason] ?? skill.action.disabledReason : null;
  return <section className="skill-enhance-panel" aria-label={`${skill.name} ${visible}`}>
    <strong className="skill-enhance-label">필요 재료</strong>
    <SkillResourceCost skill={skill} riceBalance={riceBalance} />
    <p className="skill-success-rate"><b>{rate(skill.action.successBasisPoints)}</b><small>성공률</small></p>
    <button type="button" className={commandKind === "promote" ? "skill-promote-button" : "skill-enhance-button"} aria-label={accessible} disabled={!skill.action.executable || busy || blocked} onClick={() => onCommand(commandKind, skill.skillId)}>{ACTION_BUTTON_SUFFIXED.has(visible) ? `${visible}하기` : visible}</button>
  </section>;
}

export function SkillWindow({ open, onClose }: { open: boolean; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (open && !element.open) element.showModal();
    if (!open && element.open) element.close();
  }, [open]);
  return <dialog ref={dialog} className="skills-window" aria-labelledby="skills-title" onCancel={event => { event.preventDefault(); onClose(); }} onMouseDown={event => { if (event.currentTarget === event.target) onClose(); }}><SkillsScreen onClose={onClose} /></dialog>;
}

export function SkillsScreen({ onClose }: { onClose?: () => void } = {}) {
  const queryClient = useQueryClient();
  // 강화 결과는 창 안의 줄이 아니라 팝업으로만 알린다. 줄로 띄우면 그만큼 카드가 밀린다.
  const [notice, setRawNotice] = useState<SkillNotice | null>(null);
  /*
   * 같은 결과가 잇달아 나와도 매번 다시 보여야 한다. 번호를 붙여 알림을 새로
   * 띄우지 않으면, 강화 실패가 두 번 연달아 났을 때 두 번째는 아무 일도 없는 것처럼
   * 지나간다. 누르는 속도가 결과보다 빠를 때 정보가 안 뜨던 이유다.
   */
  const noticeSequence = useRef(0);
  const setNotice = (next: Omit<SkillNotice, "id"> | null) => {
    noticeSequence.current += 1;
    setRawNotice(next && { ...next, id: noticeSequence.current });
  };
  const [lastCommand, setLastCommand] = useState<SkillCommand | null>(null);
  const [lastLoadout, setLastLoadout] = useState<SkillLoadoutCommand | null>(null);
  const [selectingSlot, setSelectingSlot] = useState<number | null>(null);
  /* 고른 스킬은 이름으로 들고 있다가 목록에서 찾는다. 목록이 바뀌어도 첫 장으로 물러설 뿐이다. */
  const [pickedSkillId, setPickedSkillId] = useState<string | null>(null);
  const state = useQuery({ queryKey: ["skills"], queryFn: skillsApi.state, retry: false });
  const command = useMutation({ mutationFn: skillsApi.command, retry: false, onSuccess: async (result, variables) => {
    const promoting = variables.kind === "promote";
    const label = promoting ? "승급" : "강화";
    setNotice(result.success
      /* 승급은 드문 일이라 등급까지 읽고 갈 시간을 준다. */
      ? { title: promoting ? `승급 성공 · ${result.skill.gradeName}` : "강화 성공", message: `${result.skill.name} ${result.skill.gradeName} +${result.skill.level}`, tone: "success", duration: promoting ? 2600 : undefined }
      : { title: `${label} 실패`, message: `다음 성공률 ${rate(result.skill.action.successBasisPoints)}`, tone: "failure" }); queryClient.setQueryData<SkillState>(["skills"], result.state); void Promise.all([queryClient.invalidateQueries({ queryKey: ["inventory"] }), queryClient.invalidateQueries({ queryKey: ["auth", "session"] }), queryClient.invalidateQueries({ queryKey: ["character-stats"] })]); }, onError: error => { if (!isUncertainSkillCommandError(error)) void state.refetch(); setNotice(isUncertainSkillCommandError(error) ? { title: "확인 필요", message: "변경 결과를 확인하지 못했습니다. 같은 요청으로 다시 확인해 주세요.", retry: "command" } : { title: "알림", message: ERROR_LABELS[error instanceof Error ? error.message : ""] ?? "스킬 명령을 처리하지 못했습니다." }); } });
  const loadout = useMutation({ mutationFn: ({ ids, key }: SkillLoadoutCommand) => skillsApi.updateLoadout(ids, key), retry: false, onSuccess: next => { setLastLoadout(null); queryClient.setQueryData<SkillState>(["skills"], next); }, onError: error => { if (!isUncertainSkillCommandError(error)) void state.refetch(); setNotice(isUncertainSkillCommandError(error) ? { title: "확인 필요", message: "장착 결과를 확인하지 못했습니다. 같은 요청으로 다시 확인해 주세요.", retry: "loadout" } : { title: "알림", message: error instanceof Error ? error.message : "장착 저장 실패" }); } });
  const data = state.data;
  const pickedSkill = data?.skills.find(skill => skill.skillId === pickedSkillId) ?? data?.skills[0] ?? null;
  const loadoutUncertain = loadout.isError && isUncertainSkillCommandError(loadout.error);
  const commandUncertain = command.isError && isUncertainSkillCommandError(command.error);
  const busy = command.isPending || loadout.isPending || loadoutUncertain || commandUncertain;
  const contentKey = data?.skills.map(skill => `${skill.skillId}:${skill.grade}:${skill.level}:${skill.effectText}:${skill.equippedSlot}`).join("|") ?? "loading";
  const layoutRoot = usePretextCardLayout(contentKey);
  const bookBalance = useMemo(() => {
    const books = new Map<string, number>();
    data?.skills.forEach(skill => skill.action.books.forEach(book => books.set(book.itemId, Math.max(books.get(book.itemId) ?? 0, book.availableQuantity))));
    return [...books.values()].reduce((sum, quantity) => sum + quantity, 0);
  }, [data]);
  const saveLoadout = (ids: string[]) => {
    const request = { ids, key: crypto.randomUUID() };
    setLastLoadout(request); loadout.mutate(request);
  };
  const handleSlotClick = (index: number, skill?: SkillSummary) => {
    if (!data || busy) return;
    if (skill) {
      setSelectingSlot(null);
      saveLoadout(data.activeLoadout.filter((_, slotIndex) => slotIndex !== index));
      return;
    }
    if (index !== data.activeLoadout.length || data.activeLoadout.length >= 4) return;
    setSelectingSlot(current => current === index ? null : index);
  };
  const selectSkill = (skill: SkillSummary) => {
    if (!data || busy || selectingSlot === null || !skill.active || !skill.unlocked || data.activeLoadout.includes(skill.skillId)) return;
    const next = [...data.activeLoadout];
    next.splice(selectingSlot, 0, skill.skillId);
    setSelectingSlot(null);
    saveLoadout(next.slice(0, 4));
  };
  const send = (kind: "enhance" | "promote", skillId: string) => {
    if (busy) return;
    const next = { kind, skillId, key: crypto.randomUUID() } satisfies SkillCommand;
    setLastCommand(next); command.mutate(next);
  };
  return <section className="skills-screen" aria-labelledby="skills-title"><div className="skills-paper" ref={layoutRoot}>
    <header className="skills-heading"><div className="skills-title"><img src={skillsTitleIcon} alt="" aria-hidden="true" /><h2 id="skills-title">스킬</h2></div>{data && <div className="skills-holdings" aria-label="보유 재화"><strong>보유 재화</strong><span><img src={riceIcon} alt="" aria-hidden="true" /><b>{data.riceBalance.toLocaleString()}</b></span><span><img src={skillbookIcon} alt="" aria-hidden="true" /><b>{bookBalance.toLocaleString()}</b></span></div>}{onClose && <button type="button" className="paper-close" aria-label="스킬 화면 닫기" onClick={onClose}><img src={closeIcon} alt="" aria-hidden="true" /></button>}</header>
    {state.isLoading && <div className="skills-message">스킬 상태를 불러오는 중입니다.</div>}{state.error && !data && <div className="skills-message error" role="alert"><strong>스킬 상태를 불러오지 못했습니다.</strong><button onClick={() => state.refetch()}>다시 시도</button></div>}
    {data && <div className="skills-body">
      <div className="skill-growth-column">
        <div className={`skill-growth-grid ${selectingSlot !== null ? "selecting" : ""}`} aria-label={selectingSlot !== null ? `${selectingSlot + 1}번 자동 사용 슬롯에 장착할 스킬 선택` : undefined}>{data.skills.map(skill => <SkillCard key={skill.skillId} skill={skill} equippedSlot={data.activeLoadout.indexOf(skill.skillId) >= 0 ? data.activeLoadout.indexOf(skill.skillId) : null} busy={busy} selecting={selectingSlot !== null} picked={pickedSkill?.skillId === skill.skillId} onPick={next => setPickedSkillId(next.skillId)} onSelect={selectSkill} />)}</div>
        {pickedSkill && <SkillEnhancePanel skill={pickedSkill} riceBalance={data.riceBalance} busy={busy} blocked={selectingSlot !== null} onCommand={send} />}
      </div>
      <SkillLoadout skills={data.skills} activeLoadout={data.activeLoadout} selectingSlot={selectingSlot} busy={busy} onSlotClick={handleSlotClick} />
    </div>}
  </div>
    {notice && !notice.retry && <NoticeToast key={notice.id} message={notice.tone ? notice.title : notice.message} tone={notice.tone} duration={notice.duration} onDone={() => setNotice(null)} />}
    {notice && notice.retry && <TopAlert message={notice.message} action={<>
      {notice.retry === "command" && lastCommand && <button type="button" disabled={command.isPending} onClick={() => { setNotice(null); command.mutate(lastCommand); }}>같은 요청 다시 확인</button>}
      {notice.retry === "loadout" && lastLoadout && <button type="button" disabled={loadout.isPending} onClick={() => { setNotice(null); loadout.mutate(lastLoadout); }}>같은 장착 요청 다시 확인</button>}
    </>} />}
  </section>;
}
