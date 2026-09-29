import "./DeploymentStatusScreen.css";

export function DeploymentStatusScreen() {
  return <main className="deployment-screen" aria-labelledby="deployment-title">
    <div className="deployment-screen__shade" aria-hidden="true" />
    <section className="deployment-notice" role="status" aria-live="polite">
      <div className="deployment-notice__pin" aria-hidden="true" />
      <p className="deployment-notice__eyebrow">HANJJAK · SYSTEM UPDATE</p>
      <div className="deployment-notice__sprout" aria-hidden="true"><i /><i /><b /></div>
      <h1 id="deployment-title">배포중입니다</h1>
      <p className="deployment-notice__copy">더 나은 한짝을 준비하고 있어요.<br />잠시 후 다시 접속해 주세요.</p>
      <div className="deployment-progress" aria-hidden="true"><i /><i /><i /></div>
      <small>서비스가 준비되면 새로고침해 주세요.</small>
    </section>
  </main>;
}
