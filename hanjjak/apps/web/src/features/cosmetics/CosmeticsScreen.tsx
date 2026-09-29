import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { cosmeticsApi, CosmeticsApiError, isCosmeticsLockedError, isUncertainCosmeticsError, type Catalog, type Collection, type DrawResponse } from "./api";
import { cosmeticsErrorMessage } from "./presentation";
import { CosmeticDrawReveal } from "./CosmeticDrawReveal";
import { CosmeticGachaBoard, CosmeticWalletBalance } from "./CosmeticGachaBoard";
import { CosmeticProbabilityDialog } from "./CosmeticProbabilityDialog";
import { CosmeticSelectorScreen } from "./CosmeticSelectorScreen";
import { TopAlert } from "../../shared/TopAlert";
import "./cosmetics.css";

// 이 화면이 보내는 명령은 뽑기와 선택 상자뿐이다.
// 등록·착용은 캐릭터 창의 치장 탭이 전담한다.
type Command =
  | { kind: "draw"; bannerId: string; count: 1 | 10; key: string }
  | { kind: "claim"; bannerId: string; count: number; key: string }
  | { kind: "openBox"; boxItemId: string; cosmeticId: string; key: string };
type CommandInput =
  | { kind: "draw"; bannerId: string; count: 1 | 10 }
  | { kind: "claim"; bannerId: string; count: number }
  | { kind: "openBox"; boxItemId: string; cosmeticId: string };
type CommandResult = { command: Command; collection?: Collection; draw?: DrawResponse };

async function execute(command: Command): Promise<CommandResult> {
  switch (command.kind) {
    case "draw": {
      const draw = await cosmeticsApi.draw(command.bannerId, command.count, command.key);
      return { command, collection: draw.collection, draw };
    }
    case "claim":
      await cosmeticsApi.claimMilestone(command.bannerId, command.count, command.key);
      return { command };
    case "openBox": return { command, collection: await cosmeticsApi.openSelectorBox(command.boxItemId, command.cosmeticId, command.key) };
  }
}

export function CosmeticsScreen({ onClose }: { onClose?: () => void } = {}) {
  const queryClient = useQueryClient();
  const [selectedBannerId, setSelectedBannerId] = useState<string | null>(null);
  const [drawResult, setDrawResult] = useState<DrawResponse | null>(null);
  const [drawResultFromBox, setDrawResultFromBox] = useState(false);
  const [revealSequence, setRevealSequence] = useState(0);
  const [claimedBannerId, setClaimedBannerId] = useState<string | null>(null);
  const [selectorBannerId, setSelectorBannerId] = useState<string | null>(null);
  const [oddsBannerId, setOddsBannerId] = useState<string | null>(null);
  const [currentCommand, setCurrentCommand] = useState<Command | null>(null);
  const [deterministicErrorRefreshing, setDeterministicErrorRefreshing] = useState(false);

  const banners = useQuery({ queryKey: ["cosmetic-banners"], queryFn: cosmeticsApi.banners, retry: false });
  const collection = useQuery({ queryKey: ["cosmetic-collection"], queryFn: cosmeticsApi.collection, retry: false });
  const catalog = useQuery({ queryKey: ["cosmetic-catalog"], queryFn: cosmeticsApi.catalog, retry: false });
  const effectiveBannerId = selectedBannerId ?? banners.data?.[0]?.bannerId ?? "";
  const detail = useQuery({ queryKey: ["cosmetic-banner-detail", effectiveBannerId], queryFn: () => cosmeticsApi.detail(effectiveBannerId), enabled: Boolean(selectedBannerId), retry: false });
  useEffect(() => {
    if (banners.error) {
      // 배너가 끊기면 그 배너를 근거로 띄운 확률 창도 같이 닫는다.
      setSelectedBannerId(null);
      setOddsBannerId(null);
    }
  }, [banners.error]);

  const updateCollection = (next: Collection) => {
    queryClient.setQueryData(["cosmetic-collection"], next);
    void queryClient.invalidateQueries({ queryKey: ["character-stats"] });
  };
  const refreshAuthoritativeState = (command: Command) => Promise.all([
    queryClient.refetchQueries({ queryKey: ["cosmetic-banners"] }),
    queryClient.refetchQueries({ queryKey: ["cosmetic-collection"] }),
    queryClient.refetchQueries({ queryKey: ["cosmetic-catalog"] }),
    ...(command.kind === "draw" ? [queryClient.refetchQueries({ queryKey: ["cosmetic-banner-detail", command.bannerId] })] : []),
    queryClient.refetchQueries({ queryKey: ["character-stats"] }),
  ]).then(() => undefined);
  const selectorBoxBannerId = useRef<string | null>(null);
  const inFlight = useRef(false);
  const deterministicErrorRefreshingRef = useRef(false);
  const mutation = useMutation({ mutationFn: execute, retry: false, onSuccess: (result) => {
    if (result.collection) updateCollection(result.collection);
    if (result.command.kind === "draw") {
      setDrawResultFromBox(false);
      setDrawResult(result.draw ?? null);
      setRevealSequence((current) => current + 1);
      setClaimedBannerId(null);
      void queryClient.invalidateQueries({ queryKey: ["cosmetic-banner-detail", result.command.bannerId] });
    } else if (result.command.kind === "claim") {
      setClaimedBannerId(result.command.bannerId);
      selectorBoxBannerId.current = result.command.bannerId;
      setSelectorBannerId(result.command.bannerId);
    }
    else if (result.command.kind === "openBox") {
      // 선택 상자로 받은 치장도 1회 뽑기와 같은 연출로 보여 준다.
      const chosenId = result.command.cosmeticId;
      const definition = queryClient.getQueryData<Catalog>(["cosmetic-catalog"])?.cosmetics.find((item) => item.cosmeticId === chosenId);
      setSelectorBannerId(null);
      if (result.collection && definition) {
        const owned = result.collection.states.find((state) => state.cosmeticId === chosenId);
        setDrawResult({
          bannerId: selectorBoxBannerId.current ?? "",
          ticketCost: 0,
          riceCost: 0,
          results: [{ cosmeticId: chosenId, grade: definition.grade, isNew: (owned?.registeredQuantity ?? 0) + (owned?.unregisteredQuantity ?? 0) <= 1 }],
          collection: result.collection,
        });
        setDrawResultFromBox(true);
        setRevealSequence((current) => current + 1);
      }
    }
    void queryClient.invalidateQueries({ queryKey: ["cosmetic-banners"] });
    setCurrentCommand(null);
  }, onError: (error, command) => {
    setCurrentCommand(command);
    if (command.kind === "draw" || command.kind === "claim") setDrawResult(null);
    if (command.kind === "openBox") setSelectorBannerId(null);
  }, onSettled: () => {
    inFlight.current = false;
  } });
  const currentMutationUncertain = mutation.isError && isUncertainCosmeticsError(mutation.error);
  const drawMutationGachaUnavailable = mutation.isError && currentCommand?.kind === "draw" && mutation.error instanceof CosmeticsApiError && mutation.error.code === "GACHA_CONTENT_UNAVAILABLE";
  const gachaUnavailable = Boolean(banners.error) || drawMutationGachaUnavailable;
  const deterministicErrorOpen = mutation.isError && !currentMutationUncertain;
  const blocked = mutation.isPending || deterministicErrorRefreshing || deterministicErrorOpen || currentMutationUncertain;
  const run = (command: CommandInput | Command) => {
    if (inFlight.current || deterministicErrorRefreshingRef.current || blocked || (gachaUnavailable && command.kind === "draw")) return;
    const next = ("key" in command ? command : { ...command, key: crypto.randomUUID() }) as Command;
    if (next.kind === "claim") setClaimedBannerId(null);
    inFlight.current = true;
    setCurrentCommand(next);
    mutation.mutate(next);
  };
  const currentRetryCommand = currentMutationUncertain ? currentCommand : null;
  const retry = (command: Command | null) => {
    if (!command || inFlight.current || mutation.isPending || (gachaUnavailable && command.kind === "draw")) return;
    inFlight.current = true;
    setCurrentCommand(command);
    mutation.mutate(command);
  };
  const retrySameCommand = () => retry(currentRetryCommand);

  if (collection.isLoading || catalog.isLoading || (banners.isLoading && !banners.data)) return <p>치장 정보를 불러오는 중입니다.</p>;
  if (isCosmeticsLockedError(collection.error) || isCosmeticsLockedError(catalog.error)) return <section className="cosmetic-screen cosmetic-locked-notice" aria-labelledby="cosmetics-locked-title">
    <h2 id="cosmetics-locked-title">치장 시스템 잠금</h2>
    <p role="alert">스테이지 1-5를 최초 클리어하면 치장 뽑기와 도감이 해금됩니다.</p>
  </section>;
  if (collection.error || catalog.error) return <p role="alert">치장 정보를 불러오지 못했습니다.</p>;
  const collectionData = collection.data!;
  const catalogData = catalog.data!;
  const showCurrentUncertainRetry = currentMutationUncertain;
  const closeCurrentError = async () => {
    if (deterministicErrorRefreshingRef.current) return;
    deterministicErrorRefreshingRef.current = true;
    setDeterministicErrorRefreshing(true);
    try {
      if (currentCommand && !currentMutationUncertain) await refreshAuthoritativeState(currentCommand);
    } finally {
      mutation.reset();
      setCurrentCommand(null);
      deterministicErrorRefreshingRef.current = false;
      setDeterministicErrorRefreshing(false);
    }
  };
  const headlineBanner = banners.data?.[0];
  const resultBanner = drawResult ? banners.data?.find((banner) => banner.bannerId === drawResult.bannerId) : undefined;
  const selectorBanner = selectorBannerId ? banners.data?.find((banner) => banner.bannerId === selectorBannerId) : undefined;
  if (selectorBanner) return <CosmeticSelectorScreen banner={selectorBanner} catalog={catalogData} pending={blocked} onBack={() => setSelectorBannerId(null)} onSelect={(cosmeticId) => run({ kind: "openBox", boxItemId: selectorBanner.milestone.boxItemId, cosmeticId })} />;
  if (drawResult) return <section key="reveal" className="cosmetic-screen cosmetic-reveal-screen" aria-label="치장 뽑기 결과 화면">
    {/* 갓 뽑은 것을 바로 입어 보러 갈 수 있게. 닫고 나면 캐릭터 창의 치장 탭이 열린다. */}
    {onClose && <button type="button" className="cosmetic-dress-up" onClick={onClose}>치장하러 가기</button>}
    <CosmeticDrawReveal key={revealSequence} draw={drawResult} catalog={catalogData} banner={resultBanner} pending={blocked} milestoneClaimed={claimedBannerId === drawResult.bannerId} onRedraw={drawResultFromBox ? undefined : (count) => run({ kind: "draw", bannerId: drawResult.bannerId, count })} continueLabel={drawResultFromBox ? "확인" : undefined} onClaimMilestone={resultBanner?.milestone.claimableBoxCount ? () => run({ kind: "claim", bannerId: drawResult.bannerId, count: 1 }) : undefined} onOpenSelector={resultBanner?.milestone.ownedBoxQuantity ? () => setSelectorBannerId(drawResult.bannerId) : undefined} onContinue={() => { setDrawResult(null); setDrawResultFromBox(false); setClaimedBannerId(null); }} />
  </section>;
  return <section key="main" className="cosmetic-screen" aria-labelledby="cosmetics-title">
    {onClose && <button type="button" className="cosmetic-return" aria-label="치장 뽑기 닫기" onClick={onClose}>×</button>}
    {/* 뽑고 나면 받은 것을 보러 가고 싶다. 닫기를 찾아 헤매지 않게 지름길을 둔다. */}
    {onClose && <button type="button" className="cozy-quick-shortcut" onClick={onClose}>치장 보러 가기<i className="cozy-chevron is-next" aria-hidden="true" /></button>}
    <header className="cosmetic-screen-heading">
      {headlineBanner && !gachaUnavailable ? <button type="button" className="cosmetic-odds-button" onClick={() => { setSelectedBannerId(headlineBanner.bannerId); setOddsBannerId(headlineBanner.bannerId); }}><i aria-hidden="true" />등급별 확률</button> : <span />}
      <h2 id="cosmetics-title">치장 뽑기</h2>
      {headlineBanner && !gachaUnavailable ? <CosmeticWalletBalance banner={headlineBanner} /> : <span />}
    </header>
    {showCurrentUncertainRetry
      ? <TopAlert
        message="변경 결과를 확인하지 못했습니다. 같은 요청을 다시 확인하기 전까지 새 변경을 제한합니다."
        action={<button type="button" disabled={mutation.isPending || (gachaUnavailable && currentRetryCommand?.kind === "draw")} onClick={retrySameCommand}>같은 요청 다시 확인</button>} />
      : mutation.isError && <TopAlert
        message={cosmeticsErrorMessage(mutation.error instanceof Error ? mutation.error.message : "")}
        action={<button type="button" onClick={closeCurrentError}>오류 닫기</button>} />}
    {gachaUnavailable ? <div className="cosmetic-error cosmetic-gacha-unavailable" role="status"><strong>현재 뽑기를 이용할 수 없습니다.</strong><p>도감·등록·착용은 캐릭터 창의 치장 탭에서 계속 이용할 수 있습니다.</p></div> : banners.data && <CosmeticGachaBoard banners={banners.data} catalog={catalogData} pending={blocked} claimedBannerId={claimedBannerId} onDraw={(banner, count) => { setSelectedBannerId(banner.bannerId); run({ kind: "draw", bannerId: banner.bannerId, count }); }} onOpenSelector={(banner) => { selectorBoxBannerId.current = banner.bannerId; setSelectorBannerId(banner.bannerId); }} onCommand={run} />}
    {oddsBannerId && <CosmeticProbabilityDialog detail={detail.data?.banner.bannerId === oddsBannerId ? detail.data : undefined} error={Boolean(detail.error)} onRetry={() => void detail.refetch()} onClose={() => setOddsBannerId(null)} />}
  </section>;
}
