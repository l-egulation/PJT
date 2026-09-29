import { useEffect, useRef, useState, type RefObject } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { characterApi, clearExpiredSession, executeCharacterCommands, isUncertainCommandError, type CharacterCollection, type CharacterCommand, type CharacterStats, type CosmeticCatalog } from "./api";
import { TopAlert } from "../../shared/TopAlert";
import { CharacterAbilityTab } from "./CharacterAbilityTab";
import { CostumePanel, GalleryPanel } from "./CosmeticPanels";
import { CharacterLoadPanel, CharacterWindowHeader, type CharacterTab } from "./CharacterWindowChrome";
import { NoticeDialog } from "../../shared/NoticeDialog";
import { rankingApi } from "../ranking/api";
import "./character.css";

type Props = {
  open: boolean;
  initialTab?: "stats" | "cosmetics";
  previewData?: { stats: CharacterStats; collection: CharacterCollection; catalog: CosmeticCatalog };
  triggerRef?: RefObject<HTMLButtonElement | null>;
  onClose: () => void;
  onGacha: () => void;
};

export function CharacterWindow({ open, initialTab = "stats", previewData, triggerRef, onClose, onGacha }: Props) {
  const client = useQueryClient();
  const dialog = useRef<HTMLDialogElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  const [tab, setTab] = useState<CharacterTab>(initialTab);
  const [lastCommands, setLastCommands] = useState<CharacterCommand[] | null>(null);
  const inFlight = useRef(false);
  const stats = useQuery({ queryKey: ["character-stats"], queryFn: previewData ? () => Promise.resolve(previewData.stats) : characterApi.stats, enabled: open, retry: false });
  const needsCosmetics = open && tab !== "stats" && stats.data?.cosmeticsUnlocked === true;
  const collection = useQuery({ queryKey: ["cosmetic-collection"], queryFn: previewData ? () => Promise.resolve(previewData.collection) : characterApi.collection, enabled: needsCosmetics, retry: false });
  const catalog = useQuery({ queryKey: ["cosmetic-catalog"], queryFn: previewData ? () => Promise.resolve(previewData.catalog) : characterApi.catalog, enabled: needsCosmetics, retry: false });
  // 전투력은 랭킹이 이미 계산해 둔 값이다. 한 줄만 쓰므로 한 명치만 받는다. 랭킹 화면은
  // 스무 명을 받으니 열쇠를 따로 둬야 서로의 캐시를 덮어쓰지 않는다.
  const power = useQuery({ queryKey: ["combat-power", "me"], queryFn: () => rankingApi.combatPower(1), enabled: open && tab === "stats", retry: false, staleTime: 15_000 });
  const mutation = useMutation({ mutationKey: ["character-command"], mutationFn: (commands: CharacterCommand[]) => executeCharacterCommands(client, commands), retry: false, onSettled: () => { inFlight.current = false; } });
  const uncertainCommand = mutation.isError && isUncertainCommandError(mutation.error);
  const commandsBlocked = mutation.isPending || uncertainCommand;

  useEffect(() => {
    if (!open) return;
    setTab(initialTab);
    if (!previewData) void client.invalidateQueries({ queryKey: ["character-stats"] });
    const previous = triggerRef?.current ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    const element = dialog.current;
    element?.showModal();
    closeButton.current?.focus();
    return () => { element?.close(); previous?.focus(); };
  }, [open, initialTab, client, previewData, triggerRef]);

  useEffect(() => {
    for (const error of [stats.error, collection.error, catalog.error]) clearExpiredSession(client, error);
  }, [client, stats.error, collection.error, catalog.error]);

  const mismatched = Boolean(collection.data && catalog.data && collection.data.contentVersion !== catalog.data.contentVersion);
  // One coordinated refresh per observed version pair; a persistent mismatch stays visible.
  const mismatchPair = mismatched ? `${collection.data!.contentVersion}/${catalog.data!.contentVersion}` : "";
  useEffect(() => {
    if (!needsCosmetics || !mismatchPair) return;
    void Promise.all([client.invalidateQueries({ queryKey: ["cosmetic-collection"] }), client.invalidateQueries({ queryKey: ["cosmetic-catalog"] })]);
  }, [client, needsCosmetics, mismatchPair]);

  const runMany = (commands: CharacterCommand[]) => {
    if (commands.length === 0) return;
    if (inFlight.current) return;
    inFlight.current = true;
    setLastCommands(commands);
    mutation.mutate(commands);
  };
  const run = (command: CharacterCommand) => runMany([command]);
  const refresh = () => {
    void stats.refetch();
    if (needsCosmetics) { void collection.refetch(); void catalog.refetch(); }
  };
  const switchTab = (next: CharacterTab) => setTab(next);
  const queryError = stats.error || (needsCosmetics && (collection.error || catalog.error));
  return <dialog ref={dialog} className="character-window" aria-labelledby="character-title" onCancel={event => { event.preventDefault(); onClose(); }} onKeyDown={event => {
    if (event.key !== "Tab") return;
    const focusable = Array.from(event.currentTarget.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])')).filter(element => element.tabIndex >= 0 && element.getClientRects().length > 0);
    const first = focusable[0], last = focusable.at(-1);
    if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
    else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
  }}>
    {open && <>
      {/* 서버 문제는 창 안이 아니라 화면 맨 위에 뜬다. 창 배치가 밀리지 않는다. */}
      {mutation.isError && <TopAlert
        message={uncertainCommand
          ? "변경 결과를 확인하지 못했습니다. 같은 요청으로 결과를 확인하기 전까지 새 변경은 잠시 제한합니다."
          : "변경 조건이 충족되지 않아 적용하지 못했습니다. 최신 상태를 확인해 주세요."}
        action={uncertainCommand
          ? <button type="button" onClick={() => lastCommands && runMany(lastCommands)}>같은 요청 다시 확인</button>
          : <button type="button" onClick={() => { refresh(); mutation.reset(); }}>상태 다시 불러오기</button>}
      />}
      <CharacterWindowHeader activeTab={tab} closeButton={closeButton} data={stats.data} onClose={onClose} onTabChange={switchTab} />
      <div className="character-body" role="tabpanel" id={`character-panel-${tab}`} aria-labelledby={`character-tab-${tab}`}>
        {queryError ? <CharacterLoadPanel kind="error" onRetry={refresh} /> : !stats.data ? <CharacterLoadPanel kind="loading" /> : tab === "stats" ? <CharacterAbilityTab data={stats.data} equipment={collection.data?.equipment} catalog={catalog.data} combatPower={power.data?.myEntry?.combatPower ?? null} /> : !stats.data.cosmeticsUnlocked ? <p className="character-notice">치장·도감은 1-5 최초 클리어 후 이용할 수 있습니다.</p> : !collection.data || !catalog.data ? <CharacterLoadPanel kind="loading" /> : mismatched ? <div role="alert" className="character-notice"><p>콘텐츠 버전이 달라 최신 정보를 확인해야 합니다.</p><button onClick={refresh}>다시 불러오기</button></div> : tab === "cosmetics" ? <CostumePanel collection={collection.data} catalog={catalog.data} pending={commandsBlocked} send={command => run({ ...command, key: crypto.randomUUID() })} sendBatch={commands => runMany(commands.map(command => ({ ...command, key: crypto.randomUUID() })))} onGacha={onGacha} /> : <GalleryPanel collection={collection.data} catalog={catalog.data} pending={commandsBlocked} send={command => run({ ...command, key: crypto.randomUUID() })} />}
      </div>
      {mutation.isError && (uncertainCommand
        ? <NoticeDialog
          title="확인 필요"
          message="변경 결과를 확인하지 못했습니다. 같은 요청으로 결과를 확인하기 전까지 새 변경은 잠시 제한합니다."
          actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={() => lastCommands && runMany(lastCommands)}>같은 요청 다시 확인</button>}
        />
        : <NoticeDialog
          message="변경 조건이 충족되지 않아 적용하지 못했습니다. 최신 상태를 확인해 주세요."
          actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={() => { refresh(); mutation.reset(); }}>상태 다시 불러오기</button>}
        />)}
    </>}
  </dialog>;
}
