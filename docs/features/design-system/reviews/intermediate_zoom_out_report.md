# Stage 8: Intermediate Zoom Out 보고서

- 대상: `docs/features/design-system/design.md` (전체, 문서 변경 없음)
- 검토자: egm-zoom-out (새 문맥, 이전 보고서 셋을 읽고 그 밖의 것만 지적, 2026-11-19)
- 판단: 수정 후 통과. Blocker 없음, Major 3, Nit 3.

| ID | 등급 | 지적 | 제안 |
|---|---|---|---|
| ZO2-M1 | Major | shadcn을 고른 근거("복사해 소유하므로 맞춤 부품을 고칠 수 있다")가 성립하지 않는다. 맞춤 부품은 모두 `ds`에서 새로 쓰고 shadcn은 Button, Dialog, Popover, Tooltip, Tabs, Toast 여섯 개를 감쌀 뿐이다. 대가로 CLI 덮어쓰기 위험, `components.json`, cva·clsx·tailwind-merge 같은 의존성, 두 층 규칙이 생긴다. Button 변형도 shadcn 기본(`default`, `destructive`, `outline`)과 달라 결국 고쳐 쓴다 | 실제 근거("접근성 기본값을 갖춘 스타일 부품을 빨리 얻는다")로 바꾸거나, `@base-ui/react`를 직접 쓰고 `ui` 층과 CLI를 없앤다 |
| ZO2-M2 | Major | 반려는 이미 두 단계(사유 입력 → 반려 확정)인데 Dialog를 더하면 세 단계다. 사유 입력을 Dialog로 옮기면 7절 "Base UI에 글자 입력 부품을 쓰지 않는다"와 부딪힌다. "수정 후 승인" 경로가 빠졌다. 결정 뒤 `Detail`이 `key`로 다시 마운트돼 화면 안 토스트는 사라지므로 Toast 제공자를 루트 레이아웃(서버 컴포넌트, `cacheComponents`)에 둬야 한다 | Dialog는 승인과 수정 후 승인에만. 반려는 기존 두 단계. 결과 안내는 `aria-live` Callout으로 하고 Toast는 뺀다. 유지하면 제공자 위치를 적는다 |
| ZO2-M3 | Major | DS-12는 지금 없는 Enter 전송을 새로 만들고 그 위험을 막는 구조다. 현재는 버튼으로만 보내 IME 문제가 없다. 잘못 보내면 하루 질문 한도를 깎는다 | 이번 범위에서 DS-12를 뺀다. 넣으려면 명세 1절에 사용자 문제를 먼저 적는다 |
| ZO2-N1 | Nit | `ProviderKind`에 `none`이 있는데 설계에 없다(그대로면 영어 `none` 노출). `isReplay` 덧붙이기(ReviewClient 27행)와 모르는 공급자 → fallback(reviewChat 43행)이 흩어져 있다 | 4.2에 출처 행 추가, `none`은 그리지 않음, 매핑을 한 곳으로 |
| ZO2-N2 | Nit | 역할 전환을 헤더로 옮기면 화면 다섯 곳에서 RoleSwitch를 빼야 한다 | 3.3에 적는다 |
| ZO2-N3 | Nit | 이미 최적화한 WebP에 `next/image`는 할 일이 거의 없고 Vercel 이미지 최적화 사용량만 쓴다. 기본 `favicon.ico`가 남아 새 아이콘과 같이 나간다 | `unoptimized` 또는 `<img>`, `favicon.ico` 삭제 |
