import { NoticeDialog } from "../../shared/NoticeDialog";

export function ConnectionLostDialog({ reason, onReconnect }: { reason: string; onReconnect: () => void }) {
  return <NoticeDialog
    title="연결이 끊겼어요"
    message="서버와의 전투 연결이 잠시 멈췄습니다. 다시 연결하면 하던 곳에서 이어집니다."
    detail={reason}
    actions={<button type="button" className="notice-dialog-primary" autoFocus onClick={onReconnect}>다시 연결</button>}
  />;
}
