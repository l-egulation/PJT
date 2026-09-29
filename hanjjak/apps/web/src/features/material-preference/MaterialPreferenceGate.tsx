import { useEffect, useState, type ReactNode } from "react";
import { LoadingScene } from "../../shared/LoadingScene";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import cornCountry from "./assets-cozy-pixel/country-corn-cozy-v2.png";
import populationIcon from "./assets-cozy-pixel/material-population-icon.png";
import selectedStamp from "./assets-cozy-pixel/material-selected-ink-stamp.png";
import potatoCountry from "./assets-cozy-pixel/country-potato-cozy-v2.png";
import sweetPotatoCountry from "./assets-cozy-pixel/country-sweet-potato-cozy-v2.png";
import sproutIcon from "./assets-cozy-pixel/sprout-icon-32.png";
import cornItemIcon from "./assets/corn-item-pixel-192.png";
import potatoItemIcon from "./assets/potato-item-pixel-192.png";
import sweetPotatoItemIcon from "./assets/sweet-potato-item-pixel-192.png";
import { materialPreferenceApi, type MaterialOption, type MaterialPreferenceState, type MaterialType } from "./api";
import "./MaterialPreferenceGate.css";

const RATE_LABELS: Record<MaterialType, string> = {
  POTATO: "감자",
  SWEET_POTATO: "고구마",
  CORN: "옥수수",
};

const MATERIAL_IMAGES: Record<MaterialType, string> = {
  POTATO: potatoCountry,
  SWEET_POTATO: sweetPotatoCountry,
  CORN: cornCountry,
};

const MATERIAL_ITEM_ICONS: Record<MaterialType, string> = {
  POTATO: potatoItemIcon,
  SWEET_POTATO: sweetPotatoItemIcon,
  CORN: cornItemIcon,
};

const COUNTRY_NAMES: Record<MaterialType, string> = {
  POTATO: "감자의 나라",
  SWEET_POTATO: "고구마의 나라",
  CORN: "옥수수의 나라",
};

const COUNTRY_MATERIAL_NAMES: Record<MaterialType, string> = {
  POTATO: "감자",
  SWEET_POTATO: "고구마",
  CORN: "옥수수",
};

const MATERIAL_DISPLAY_ORDER: MaterialType[] = ["POTATO", "SWEET_POTATO", "CORN"];

export function populationLabel(population?: number | null) {
  return population == null ? "집계 준비 중" : `${population.toLocaleString("ko-KR")}명`;
}

function selectionErrorMessage(error: Error | null) {
  const code = (error as Error & { code?: string } | null)?.code;
  if (code === "MATERIAL_ALREADY_SELECTED") return "이미 다른 나라가 확정되었습니다. 저장된 선택을 다시 확인합니다.";
  if (code === "INVALID_MATERIAL_TYPE") return "선택할 수 없는 나라입니다. 다른 나라를 선택해 주세요.";
  if (code === "AUTHENTICATION_REQUIRED") return "로그인 시간이 만료되었습니다. 다시 로그인해 주세요.";
  return "선택을 확정하지 못했습니다. 잠시 후 다시 시도해 주세요.";
}

function MaterialCard({ option, selected, disabled, populationCount, onSelect }: {
  option: MaterialOption;
  selected: boolean;
  disabled: boolean;
  populationCount: number;
  onSelect: () => void;
}) {
  const displayedRates: Array<[MaterialType, number]> = MATERIAL_DISPLAY_ORDER.map((type) => [
    type,
    option.dropRates[type],
  ]);
  const rateLabel = displayedRates.map(([type, rate]) => `${RATE_LABELS[type]} ${rate}%`).join(", ");

  return (
    <button
      type="button"
      className={`material-card${selected ? " selected" : ""}`}
      aria-label={`${COUNTRY_NAMES[option.materialType]}, 인구 ${populationLabel(populationCount)}`}
      aria-pressed={selected}
      disabled={disabled}
      onClick={onSelect}
    >
      <span className="material-card__heading">
        <span className="material-card__title">
          <span className="material-card__name">{COUNTRY_MATERIAL_NAMES[option.materialType]}</span>의 나라
        </span>
        <span className="material-card__population">
          <img src={populationIcon} alt="" aria-hidden="true" />
          <span>{populationLabel(populationCount)}</span>
        </span>
      </span>
      <span className="material-card__art" aria-hidden="true">
        <img src={MATERIAL_IMAGES[option.materialType]} alt="" />
        <span className="material-card__selection-stamp">
          <img src={selectedStamp} alt="" />
          <strong>선택</strong>
        </span>
      </span>
      <span className="material-card__info" aria-label={rateLabel}>
        <span className="material-card__rates" aria-hidden="true">
          {displayedRates.map(([type, rate]) => (
            <span
              className={`material-card__rate${type === option.materialType ? " material-card__rate--primary" : ""}`}
              key={type}
            >
              <img src={MATERIAL_ITEM_ICONS[type]} alt="" />
              <strong>{rate}%</strong>
            </span>
          ))}
        </span>
      </span>
    </button>
  );
}

function MaterialStatus({ error, onRetry }: { error?: boolean; onRetry?: () => void }) {
  return <LoadingScene
    message={error ? "한짝을 불러오지 못했습니다." : "한짝을 불러오는 중입니다."}
    action={error ? <button type="button" onClick={onRetry}>다시 불러오기</button> : undefined}
  />;
}

export function MaterialPreferenceGate({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [selectedType, setSelectedType] = useState<MaterialType | null>(null);
  const [confirmationOpen, setConfirmationOpen] = useState(false);
  const preference = useQuery({
    queryKey: ["material-preference"],
    queryFn: materialPreferenceApi.get,
    retry: false,
  });
  const select = useMutation({
    mutationFn: materialPreferenceApi.select,
    onSuccess: (result) => {
      setConfirmationOpen(false);
      queryClient.setQueryData<MaterialPreferenceState>(["material-preference"], (current) => current ? {
        ...current,
        selected: true,
        primaryMaterialType: result.primaryMaterialType,
      } : current);
      void queryClient.invalidateQueries({ queryKey: ["material-preference"] });
    },
    onError: () => {
      setConfirmationOpen(false);
      void queryClient.invalidateQueries({ queryKey: ["material-preference"] });
    },
  });

  useEffect(() => {
    if (!confirmationOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !select.isPending) setConfirmationOpen(false);
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [confirmationOpen, select.isPending]);

  if (preference.isLoading) return <MaterialStatus />;
  if (preference.error || !preference.data) return <MaterialStatus error onRetry={() => void preference.refetch()} />;
  if (preference.data.selected) return children;

  const selectedOption = preference.data.options.find((option) => option.materialType === selectedType) ?? null;
  const orderedOptions = MATERIAL_DISPLAY_ORDER.flatMap((materialType) => (
    preference.data.options.filter((option) => option.materialType === materialType)
  ));

  function choose(materialType: MaterialType) {
    select.reset();
    setConfirmationOpen(false);
    setSelectedType(materialType);
  }

  return (
    <main className="material-scene" aria-labelledby="material-title">
      <div className="material-scene__shade" aria-hidden="true" />
      <section className="material-board">
        <header className="material-board__header">
          <h1 id="material-title">
            <img className="material-title__sprig" src={sproutIcon} alt="" aria-hidden="true" />
            <span>함께할 <em>나라</em>를 선택하세요</span>
            <img className="material-title__sprig material-title__sprig--right" src={sproutIcon} alt="" aria-hidden="true" />
          </h1>
        </header>

        <div className={`material-options${selectedOption ? " has-selection" : ""}`} role="group" aria-label="시작 국가 선택">
          {orderedOptions.map((option) => (
            <MaterialCard
              key={option.materialType}
              option={option}
              selected={selectedType === option.materialType}
              disabled={select.isPending}
              populationCount={preference.data.populationCounts[option.materialType] ?? 0}
              onSelect={() => choose(option.materialType)}
            />
          ))}
        </div>

        <div className="material-confirm">
          <button
            type="button"
            className="material-confirm__button"
            disabled={!selectedOption || select.isPending}
            onClick={() => setConfirmationOpen(true)}
          >
            <span className="material-confirm__label">
              <i aria-hidden="true">✦</i>
              선택 확정
              <i aria-hidden="true">✦</i>
            </span>
          </button>
        </div>

        {select.error && <p className="material-error" role="alert">{selectionErrorMessage(select.error)}</p>}

        {confirmationOpen && selectedOption && (
          <div className="material-final-confirm" role="presentation">
            <section
              className="material-final-confirm__panel"
              role="dialog"
              aria-modal="true"
              aria-labelledby="material-final-confirm-title"
              aria-describedby="material-final-confirm-description"
            >
              <div className="material-final-confirm__chosen" aria-hidden="true">
                <img src={MATERIAL_ITEM_ICONS[selectedOption.materialType]} alt="" />
              </div>
              <h2 id="material-final-confirm-title">{COUNTRY_NAMES[selectedOption.materialType]}로 정할까요?</h2>
              <p id="material-final-confirm-description">
                선택한 나라는 이후 변경할 수 없어요.<br />이대로 모험을 시작할까요?
              </p>
              <div className="material-final-confirm__actions">
                <button type="button" className="secondary" autoFocus disabled={select.isPending} onClick={() => setConfirmationOpen(false)}>취소</button>
                <button type="button" className="primary" disabled={select.isPending} onClick={() => select.mutate(selectedOption.materialType)}>
                  {select.isPending ? "확정 중…" : "확정"}
                </button>
              </div>
            </section>
          </div>
        )}
      </section>
    </main>
  );
}
