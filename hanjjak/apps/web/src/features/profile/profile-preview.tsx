import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import "../../styles.css";
import { ProfileScreen } from "./ProfileScreen";
import "./profile-preview.css";

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: Number.POSITIVE_INFINITY } },
});

queryClient.setQueryData(["auth", "session"], {
  authenticated: true,
  account: {
    accountId: "preview-account",
    characterId: "preview-character",
    email: "player@example.com",
    nickname: "한짝",
    level: 24,
    experience: 294_720,
    rice: 15_820,
  },
});

function ProfilePreview() {
  const [open, setOpen] = useState(true);

  return <QueryClientProvider client={queryClient}>
    <main className="profile-preview-underlay">
      <div>
        <strong>한짝</strong>
        <span>마이페이지 모달 로컬 미리보기</span>
      </div>
      {!open && <button type="button" onClick={() => setOpen(true)}>마이페이지 다시 열기</button>}
    </main>
    <ProfileScreen open={open} onClose={() => setOpen(false)} />
  </QueryClientProvider>;
}

createRoot(document.getElementById("root")!).render(<ProfilePreview />);
