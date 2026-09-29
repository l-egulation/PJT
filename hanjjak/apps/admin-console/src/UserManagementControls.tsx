import { FormEvent, useEffect, useMemo, useState } from "react";
import { AdminUserState, createApiClient } from "@hanjjak/client-sdk";
import { adminErrorMessage } from "./presentation";
import "./UserManagementControls.css";

type AdminApi = ReturnType<typeof createApiClient>["admin"];
type AdminCatalog = Awaited<ReturnType<AdminApi["catalog"]>>;
type CatalogItem = AdminCatalog["items"][number];
type CatalogCosmetic = AdminCatalog["cosmetics"][number];
type CatalogStage = AdminCatalog["stages"][number];
type Operation = "progression" | "rice" | "item" | "gems" | "stages" | "cosmetics" | "equipment";
type Mode = "ADD" | "SET";
type EquipmentMode = "UNLOCK" | "LOCK";

export function UserManagementControls({
  api,
  management,
  onManagement,
}: {
  api: AdminApi;
  management: AdminUserState;
  onManagement: (state: AdminUserState) => void;
}) {
  const [catalog, setCatalog] = useState<AdminCatalog | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [operation, setOperation] = useState<Operation>("progression");
  const [pending, setPending] = useState(false);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [progressionLevel, setProgressionLevel] = useState(String(management.level));
  const [progressionExperience, setProgressionExperience] = useState(String(management.experience));
  const [riceMode, setRiceMode] = useState<Mode>("ADD");
  const [riceAmount, setRiceAmount] = useState("0");
  const [itemId, setItemId] = useState("");
  const [itemMode, setItemMode] = useState<Mode>("ADD");
  const [itemQuantity, setItemQuantity] = useState("1");
  const [gemLevel, setGemLevel] = useState("1");
  const [gemOption, setGemOption] = useState("FLAT_ATTACK");
  const [gemAction, setGemAction] = useState<"ADD" | "REMOVE">("ADD");
  const [gemAmount, setGemAmount] = useState("1");
  const [stageId, setStageId] = useState("");
  const [cosmeticId, setCosmeticId] = useState("");
  const [registeredQuantity, setRegisteredQuantity] = useState("1");
  const [unregisteredQuantity, setUnregisteredQuantity] = useState("0");
  const [equipmentSlot, setEquipmentSlot] = useState("WEAPON");
  const [equipmentMode, setEquipmentMode] = useState<EquipmentMode>("UNLOCK");
  const [equipmentGrade, setEquipmentGrade] = useState("NORMAL");
  const [equipmentLevel, setEquipmentLevel] = useState("1");

  useEffect(() => {
    let active = true;
    setCatalogLoading(true);
    setCatalogError(null);
    api.catalog()
      .then((value) => {
        if (!active) return;
        setCatalog(value);
        setItemId((current) => current || value.items.find((item) => item.stackable)?.itemId || "");
        setStageId((current) => current || value.stages[0]?.stageId || "");
        setCosmeticId((current) => current || value.cosmetics[0]?.cosmeticId || "");
      })
      .catch((failure) => {
        if (active) setCatalogError(adminErrorMessage(failure));
      })
      .finally(() => {
        if (active) setCatalogLoading(false);
      });
    return () => { active = false; };
  }, [api]);

  const stackItems = useMemo(() => (catalog?.items ?? []).filter((item) => item.stackable), [catalog]);
  const selectedItem = stackItems.find((item) => item.itemId === itemId);
  const selectedCosmetic = catalog?.cosmetics.find((item) => item.cosmeticId === cosmeticId);
  const selectedStage = catalog?.stages.find((item) => item.stageId === stageId);
  const selectedItemState = management.items.find((item) => item.itemId === itemId);
  const selectedCosmeticState = management.cosmetics.find((item) => item.cosmeticId === cosmeticId);
  const selectedEquipment = management.equipment.find((item) => item.slot === equipmentSlot);

  useEffect(() => {
    if (operation === "item" && !itemId && stackItems[0]) setItemId(stackItems[0].itemId);
    if (operation === "stages" && !stageId && catalog?.stages[0]) setStageId(catalog.stages[0].stageId);
    if (operation === "cosmetics" && !cosmeticId && catalog?.cosmetics[0]) setCosmeticId(catalog.cosmetics[0].cosmeticId);
  }, [catalog, cosmeticId, itemId, operation, stackItems, stageId]);

  useEffect(() => {
    setProgressionLevel(String(management.level));
    setProgressionExperience(String(management.experience));
    if (selectedItemState) setItemQuantity(itemMode === "SET" ? String(selectedItemState.quantity) : "1");
    if (selectedCosmeticState) {
      setRegisteredQuantity(String(selectedCosmeticState.registeredQuantity));
      setUnregisteredQuantity(String(selectedCosmeticState.unregisteredQuantity));
    }
    if (selectedEquipment) {
      setEquipmentGrade(selectedEquipment.grade);
      setEquipmentLevel(String(selectedEquipment.enhancementLevel));
      setEquipmentMode("UNLOCK");
    }
  }, [management, selectedCosmeticState, selectedEquipment, selectedItemState]);

  function clearMessages() {
    setFeedback(null);
    setError(null);
  }

  function integer(value: string, label: string, min: number, max: number): number {
    if (!/^\d+$/.test(value)) throw new Error(`${label}은(는) 정수로 입력해 주세요.`);
    const parsed = Number(value);
    if (!Number.isSafeInteger(parsed) || parsed < min || parsed > max) throw new Error(`${label}은(는) ${min.toLocaleString()}~${max.toLocaleString()} 범위의 안전한 정수여야 합니다.`);
    return parsed;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    clearMessages();
    const trimmedReason = reason.trim();
    if (trimmedReason.length < 3) {
      setError("운영 사유를 3자 이상 입력해 주세요.");
      return;
    }
    try {
      setPending(true);
      let result: { after: AdminUserState };
      let label: string;
      switch (operation) {
        case "progression": {
          const level = integer(progressionLevel, "레벨", 1, 500);
          const experience = integer(progressionExperience, "누적 경험치", 0, Number.MAX_SAFE_INTEGER);
          result = await api.adjustUserProgression(management.accountId, level, experience, trimmedReason);
          label = "성장";
          break;
        }
        case "rice": {
          const amount = integer(riceAmount, "쌀", 0, Number.MAX_SAFE_INTEGER);
          result = await api.adjustUserRice(management.accountId, riceMode, amount, trimmedReason);
          label = "쌀";
          break;
        }
        case "item": {
          if (!selectedItem) throw new Error("지급 가능한 스택 아이템을 선택해 주세요.");
          const quantity = integer(itemQuantity, "아이템 수량", 0, Number.MAX_SAFE_INTEGER);
          result = await api.adjustUserItem(management.accountId, selectedItem.itemId, itemMode, quantity, trimmedReason);
          label = "아이템";
          break;
        }
        case "gems": {
          const level = integer(gemLevel, "보석 레벨", 1, 7);
          const amount = integer(gemAmount, "보석 수량", 1, Number.MAX_SAFE_INTEGER);
          result = await api.adjustUserGem(management.accountId, level, gemOption, gemAction === "ADD" ? amount : -amount, trimmedReason);
          label = "보석";
          break;
        }
        case "stages": {
          if (!selectedStage) throw new Error("해금할 스테이지를 선택해 주세요.");
          result = await api.unlockUserStages(management.accountId, selectedStage.stageId, trimmedReason);
          label = "스테이지";
          break;
        }
        case "cosmetics": {
          if (!selectedCosmetic) throw new Error("치장 정의를 선택해 주세요.");
          const registered = integer(registeredQuantity, "등록 수량", 0, Number.MAX_SAFE_INTEGER);
          const unregistered = integer(unregisteredQuantity, "미등록 중복 수량", 0, Number.MAX_SAFE_INTEGER);
          result = await api.adjustUserCosmetic(management.accountId, selectedCosmetic.cosmeticId, registered, unregistered, trimmedReason);
          label = "치장";
          break;
        }
        case "equipment": {
          if (equipmentMode === "LOCK") {
            result = await api.adjustUserEquipment(management.accountId, equipmentSlot, null, null, trimmedReason);
          } else {
            const level = integer(equipmentLevel, "강화 단계", 1, 30);
            result = await api.adjustUserEquipment(management.accountId, equipmentSlot, equipmentGrade, level, trimmedReason);
          }
          label = "장비";
          break;
        }
      }
      onManagement(result.after);
      setFeedback(`${label} 조정을 완료했습니다. 현재 상태를 갱신했습니다.`);
    } catch (failure) {
      setError(failure instanceof Error && !(failure instanceof TypeError) && !("code" in failure) ? failure.message : adminErrorMessage(failure));
      setPending(false);
    }
  }

  const currentState = operation === "item"
    ? selectedItemState ? `${selectedItemState.displayName}: ${selectedItemState.quantity.toLocaleString()}개 (예약 ${selectedItemState.reservedQuantity.toLocaleString()}개)` : "현재 보유하지 않음"
    : operation === "cosmetics"
      ? selectedCosmeticState ? `${selectedCosmeticState.displayName ?? selectedCosmetic?.displayName ?? cosmeticId}: 등록 ${selectedCosmeticState.registeredQuantity.toLocaleString()}개 · 미등록 ${selectedCosmeticState.unregisteredQuantity.toLocaleString()}개` : "현재 보유하지 않음"
      : operation === "equipment"
        ? selectedEquipment ? `${equipmentSlotLabel(equipmentSlot)} · ${gradeLabel(selectedEquipment.grade)} +${selectedEquipment.enhancementLevel}` : `${equipmentSlotLabel(equipmentSlot)} 잠금 상태`
        : operation === "stages" ? `최고 해금 스테이지: ${management.highestUnlockedStageId ?? "없음"}`
          : operation === "gems" ? "선택한 레벨·옵션 보석의 현재 상세 수량은 서버에서 검증합니다."
            : operation === "rice" ? `현재 보유 쌀: ${management.rice.toLocaleString()}개`
              : `현재 레벨 ${management.level} · 누적 경험치 ${management.experience.toLocaleString()}`;

  return <section className="user-management" aria-labelledby="management-title">
    <header className="user-management-heading"><div><span className="eyebrow">ACCOUNT MANAGEMENT</span><h2 id="management-title">계정 상태 조정</h2><p>서버 권한과 도메인 규칙이 최종 판단하며, 모든 변경은 감사 사유와 함께 기록됩니다.</p></div><span className="management-version">상태 버전 {management.stateVersion}</span></header>
    {catalogLoading && <p className="management-note" role="status">콘텐츠 카탈로그를 불러오는 중입니다…</p>}
    {catalogError && <div className="alert error" role="alert">{catalogError}</div>}
    {feedback && <div className="management-feedback" role="status">{feedback}</div>}
    {error && <div className="alert error" role="alert">{error}</div>}
    <div className="management-tabs" role="tablist" aria-label="계정 조정 유형">
      {([ ["progression", "성장"], ["rice", "쌀"], ["item", "아이템"], ["gems", "보석"], ["stages", "스테이지"], ["cosmetics", "치장"], ["equipment", "장비"] ] as [Operation, string][]).map(([value, label]) => <button key={value} type="button" role="tab" aria-selected={operation === value} className={operation === value ? "management-tab active" : "management-tab"} onClick={() => { setOperation(value); clearMessages(); }}>{label}</button>)}
    </div>
    <form onSubmit={submit} className="management-form">
      <section className="management-current" aria-live="polite"><strong>현재 상태</strong><span>{currentState}</span></section>
      {operation === "progression" && <fieldset><legend>성장 값</legend><p className="management-help">레벨과 누적 경험치를 함께 전송합니다. 서버의 레벨·경험치 결합 규칙을 통과하는 값만 적용됩니다.</p><div className="management-fields two"><label>레벨<input type="number" inputMode="numeric" min={1} max={500} step={1} value={progressionLevel} onChange={(event) => setProgressionLevel(event.target.value)} required /></label><label>누적 경험치<input type="number" inputMode="numeric" min={0} step={1} value={progressionExperience} onChange={(event) => setProgressionExperience(event.target.value)} required /></label></div></fieldset>}
      {operation === "rice" && <fieldset><legend>쌀 보유량</legend><ModeButtons value={riceMode} onChange={setRiceMode} addLabel="현재 보유량에 추가" setLabel="절대 보유량으로 설정" /><label>수량<input type="number" inputMode="numeric" min={0} step={1} value={riceAmount} onChange={(event) => setRiceAmount(event.target.value)} required /><small>{riceMode === "ADD" ? "추가할 양" : "변경 후 총 보유량"}</small></label></fieldset>}
      {operation === "item" && <fieldset><legend>스택 아이템</legend>{!stackItems.length && !catalogLoading ? <p className="management-help">카탈로그에 지급 가능한 스택 아이템이 없습니다.</p> : <><label>아이템<select value={itemId} onChange={(event) => setItemId(event.target.value)} required><option value="">아이템 선택</option>{stackItems.map((item) => <option key={item.itemId} value={item.itemId}>{item.displayName} · {item.itemId}</option>)}</select></label><ModeButtons value={itemMode} onChange={setItemMode} addLabel="현재 수량에 추가" setLabel="보유 수량으로 설정" /><label>수량<input type="number" inputMode="numeric" min={0} step={1} value={itemQuantity} onChange={(event) => setItemQuantity(event.target.value)} required /><small>{itemMode === "ADD" ? "추가할 양" : "변경 후 총 수량"}</small></label></>}</fieldset>}
      {operation === "gems" && <fieldset><legend>보석</legend><div className="management-fields two"><label>레벨<select value={gemLevel} onChange={(event) => setGemLevel(event.target.value)}>{Array.from({ length: 7 }, (_, index) => <option key={index + 1} value={index + 1}>레벨 {index + 1}</option>)}</select></label><label>옵션<select value={gemOption} onChange={(event) => setGemOption(event.target.value)}><option value="FLAT_ATTACK">공격력 증가</option><option value="FLAT_HP">최대 HP 증가</option><option value="ATTACK_PERCENT">공격력 비율</option><option value="FLAT_PENETRATION">방어 관통</option><option value="CRITICAL_CHANCE">치명타 확률</option><option value="HASTE">행동 속도</option></select></label></div><div className="segmented" role="group" aria-label="보석 조정"><button type="button" className={gemAction === "ADD" ? "selected positive" : ""} onClick={() => setGemAction("ADD")}>보석 추가</button><button type="button" className={gemAction === "REMOVE" ? "selected danger" : ""} onClick={() => setGemAction("REMOVE")}>보석 제거</button></div><label>수량<input type="number" inputMode="numeric" min={1} step={1} value={gemAmount} onChange={(event) => setGemAmount(event.target.value)} required /><small>항상 양수로 입력하며 선택한 동작이 방향을 결정합니다.</small></label></fieldset>}
      {operation === "stages" && <fieldset><legend>스테이지 순차 해금</legend><label>해금할 마지막 스테이지<select value={stageId} onChange={(event) => setStageId(event.target.value)} required><option value="">스테이지 선택</option>{(catalog?.stages ?? []).map((stage) => <option key={stage.stageId} value={stage.stageId}>{stage.displayName} · {stage.stageId}</option>)}</select></label><p className="management-help">선택한 스테이지까지의 정의된 순서를 서버가 검증하고 순차 해금합니다.</p></fieldset>}
      {operation === "cosmetics" && <fieldset><legend>치장 보유 상태</legend><label>치장<select value={cosmeticId} onChange={(event) => setCosmeticId(event.target.value)} required><option value="">치장 선택</option>{(catalog?.cosmetics ?? []).map((cosmetic) => <option key={cosmetic.cosmeticId} value={cosmetic.cosmeticId}>{cosmetic.displayName} · {cosmetic.slot} · {cosmetic.cosmeticId}</option>)}</select></label><div className="management-fields two"><label>등록 수량<input type="number" inputMode="numeric" min={0} step={1} value={registeredQuantity} onChange={(event) => setRegisteredQuantity(event.target.value)} required /></label><label>미등록 중복 수량<input type="number" inputMode="numeric" min={0} step={1} value={unregisteredQuantity} onChange={(event) => setUnregisteredQuantity(event.target.value)} required /></label></div></fieldset>}
      {operation === "equipment" && <fieldset><legend>장비 슬롯</legend><label>부위<select value={equipmentSlot} onChange={(event) => setEquipmentSlot(event.target.value)}>{[ ["WEAPON", "무기"], ["GLOVES", "장갑"], ["ARMOR", "갑옷"], ["HELMET", "투구"], ["CAPE", "망토"], ["SHOES", "신발"] ].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><div className="segmented" role="group" aria-label="장비 상태"><button type="button" className={equipmentMode === "UNLOCK" ? "selected positive" : ""} onClick={() => setEquipmentMode("UNLOCK")}>장비 설정</button><button type="button" className={equipmentMode === "LOCK" ? "selected danger" : ""} onClick={() => setEquipmentMode("LOCK")}>잠금으로 변경</button></div>{equipmentMode === "UNLOCK" && <div className="management-fields two"><label>등급<select value={equipmentGrade} onChange={(event) => setEquipmentGrade(event.target.value)}><option value="NORMAL">일반</option><option value="RARE">희귀</option><option value="EPIC">영웅</option><option value="LEGENDARY">전설</option></select></label><label>강화 단계<input type="number" inputMode="numeric" min={1} max={30} step={1} value={equipmentLevel} onChange={(event) => setEquipmentLevel(event.target.value)} required /></label></div>}<p className="management-help">잠금은 별도 동작이며 빈 값이나 암묵적 문자열로 표현하지 않습니다.</p></fieldset>}
      <fieldset className="reason-field"><legend>운영 사유</legend><label>사유<input value={reason} onChange={(event) => setReason(event.target.value)} minLength={3} maxLength={500} placeholder="예: 고객 지원 보상 지급" required /></label><div className="reason-presets" aria-label="사유 예시"><button type="button" onClick={() => setReason("고객 지원 보상 지급")}>고객 지원 보상 지급</button><button type="button" onClick={() => setReason("콘텐츠 검증을 위한 조정")}>콘텐츠 검증</button><button type="button" onClick={() => setReason("운영 장애 복구 조정")}>장애 복구</button></div></fieldset>
      <div className="management-actions"><button className="secondary" type="button" disabled={pending} onClick={() => { setReason(""); clearMessages(); }}>입력 초기화</button><button className="primary" type="submit" disabled={pending || catalogLoading}>{pending ? "처리 중…" : "변경 적용"}</button></div>
    </form>
  </section>;
}

function ModeButtons({ value, onChange, addLabel, setLabel }: { value: Mode; onChange: (value: Mode) => void; addLabel: string; setLabel: string }) {
  return <div className="segmented" role="group" aria-label="조정 방식"><button type="button" className={value === "ADD" ? "selected positive" : ""} onClick={() => onChange("ADD")}>{addLabel}</button><button type="button" className={value === "SET" ? "selected" : ""} onClick={() => onChange("SET")}>{setLabel}</button></div>;
}

function equipmentSlotLabel(value: string): string { return ({ WEAPON: "무기", GLOVES: "장갑", ARMOR: "갑옷", HELMET: "투구", CAPE: "망토", SHOES: "신발" } as Record<string, string>)[value] ?? value; }
function gradeLabel(value: string): string { return ({ NORMAL: "일반", RARE: "희귀", EPIC: "영웅", LEGENDARY: "전설" } as Record<string, string>)[value] ?? value; }
