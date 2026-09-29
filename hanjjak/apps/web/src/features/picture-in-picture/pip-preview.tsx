import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { PetBattleSurface } from "../battle/PetBattleSurface";
import { useBattleRuntimeStore } from "../battle/runtimeStore";
import { usePictureInPictureSessionStore } from "./pictureInPictureSessionStore";
import { DEFAULT_PIP_TRACKED_ITEM_IDS } from "./trackedItems";
import "../../styles.css";
import "./pip-preview.css";

const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Number.POSITIVE_INFINITY } } });
client.setQueryData(["auth", "session"], { authenticated: true, account: { accountId: "preview", characterId: "preview", email: "", nickname: "한짝", level: 57, experience: 1_632_261, rice: 109_213 } });
DEFAULT_PIP_TRACKED_ITEM_IDS.forEach((itemId, index) => client.setQueryData(["inventory", "item", itemId], { itemId, totalQuantity: [12_480, 88, 41, 9][index] }));
useBattleRuntimeStore.setState({ connected: true, selectedStageId: "stage.02-06", running: "cycle", settledProgression: { experienceGained: 0, riceGained: 0, levelBefore: 57, levelAfter: 57, experienceBefore: 0, experienceAfter: 0, experienceToNextLevel: 1, riceBalance: 109_213 } });
usePictureInPictureSessionStore.setState({ active: true, clearCount: 18, gainedByItemId: { POTATO_M1: 1248, POTATO_M4: 12, "skillbook:active_heavy:normal": 3, "skillbook:active_dot:rare": 1 } });

function Preview() {
  const [width, setWidth] = useState(420);
  const [frame, setFrame] = useState<number | null>(null);
  const previewStyle = { width, "--preview-frame-delay": frame === null ? "0s" : `-${frame * .1 + .01}s` } as React.CSSProperties;
  return <main className="pip-preview-page"><header><div><h1>한켠 전투창 구현 검수</h1><p>실제 React 컴포넌트 · 서버 응답과 동일한 데이터 형태</p></div><nav>{[300, 420, 560].map((value) => <button key={value} type="button" aria-pressed={width === value} onClick={() => setWidth(value)}>{value}px</button>)}</nav></header><div className="pip-preview-frames" aria-label="애니메이션 프레임 검수"><button type="button" aria-pressed={frame === null} onClick={() => setFrame(null)}>재생</button>{Array.from({ length: 12 }, (_, index) => <button key={index} type="button" aria-pressed={frame === index} onClick={() => setFrame(index)}>F{index + 1}</button>)}</div><div className="pip-preview-mat"><div className={`pip-preview-viewport ${frame === null ? "" : "is-frame-paused"}`} style={previewStyle}><PetBattleSurface /></div></div></main>;
}

createRoot(document.getElementById("root")!).render(<QueryClientProvider client={client}><Preview /></QueryClientProvider>);
