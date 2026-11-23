# 설계: 서버 깨우는 동안의 대기 화면

명세: [spec.md](spec.md) (v2). 검토: [reviews/2026-11-24-round1.md](reviews/2026-11-24-round1.md)

## 1. 파일

| 파일 | 변경 |
|---|---|
| `web/src/lib/wake.ts` | 새 파일. 화면과 무관한 순수 함수와 문구 |
| `web/src/lib/wake.test.ts` | 새 파일. vitest |
| `web/src/lib/api.ts` | `pingHealth(signal)` 추가 |
| `web/src/components/WakeWait.tsx` | 새 파일. 대기 화면 |
| `web/src/components/WakeGate.tsx` | 대기 상태를 `WakeWait`로 바꾸고, 시작 시각, 표시 지연, `/health` 핑, 완료 잠깐 보이기를 맡는다. `label: string` 필수 속성 |
| `page.tsx`, `inbox/page.tsx`, `runs/page.tsx`, `runs/[id]/RunClient.tsx`, `reviews/[id]/ReviewClient.tsx` | `label`: "승인 대기함", "알림함", "자동 처리 기록", "자동 처리 상세", "초안" |
| `web/src/app/globals.css` | `.lw-breathe`, `.lw-wiggle`, `.lw-stripes` 키프레임 |
| `docs/features/design-system/design.md`, `spec.md` | 서버 깨우기 행 갱신, DS-15에 "대기 화면에서는 버튼 안의 장식" 예외 |

## 2. `lib/wake.ts`

| 함수 | 동작 |
|---|---|
| `steps(apiUp, loaded)` | (F,F)=[active,todo], (T,F)=[done,active], (F,T)와 (T,T)=[done,done] |
| `timeFraction(ms)` | `ms/EXPECTED_MS`를 0~1로 자른다. 음수와 NaN은 0 |
| `isSlow(ms)` | `ms >= 60000` |
| `elapsedText(ms)` | 초 아래 버림. "12초째", "1분째", "1분 15초째" |
| `waitHint(ms)` | 60초 전 "보통 30초에서 1분 걸려요.", 이후 "평소보다 오래 걸리고 있어요. 조금만 더 기다려 주세요." |
| `snoreFor(taps)` | 0이면 "렉스를 눌러 보세요", 이후 `SNORES[(taps-1) % 6]` |
| `nextTip(i, dir)` | 카드 번호를 양쪽으로 돈다 |

상수: `EXPECTED_MS=60000`, `SHOW_DELAY_MS=400`, `TIP_INTERVAL_MS=7000`, `HEALTH_RETRY_MS=3000`. 완료를 보이는 시간 `DONE_HOLD_MS=600`.

## 3. `api.ts`의 `pingHealth`

```ts
/** 헤더 없는 GET이라 CORS 사전 요청이 없다. 200이면 "up", 대기 응답이면 "waking", 그 밖이면 "other". */
export async function pingHealth(signal: AbortSignal): Promise<"up" | "waking" | "other">
```

`fetch(`${BASE}/health`, { signal, cache: "no-store" })`. 네트워크 오류와 502, 503, 504는 "waking". 취소(AbortError)는 호출한 쪽이 무시한다.

## 4. `WakeGate` 흐름

상태: `res`, `attempt`, `gaveUp`, `failed`, `nonce`(기존), 그리고 `wait: { startedAt: number } | null`, `apiUp: boolean`, `holding: boolean`.

1. **요청 effect** (의존값 `nonce`): `const t0 = Date.now()`(effect 안이라 렌더 순수성 규칙에 걸리지 않음). `SHOW_DELAY_MS` 뒤 `setWait({ startedAt: t0 })`. 데이터 요청과 재시도는 지금과 같다. 데이터가 오면 표시 타이머를 지우고,
   - 대기 화면이 아직 안 보였으면 바로 `setRes`.
   - 보였으면 `setLoaded`를 위해 `setHolding(true)` 후 `DONE_HOLD_MS`(움직임 줄이기면 0) 뒤 `setRes`.
   정리 함수에서 모든 타이머를 지운다.
2. **핑 effect** (의존값 `wait`, `res`, `gaveUp`, `failed`, `holding`): `wait`가 있고 나머지가 모두 비었을 때만 돈다. `AbortController` 하나로 한 번에 요청 하나를 보낸다. "up"이면 `setApiUp(true)` 후 멈춘다. "waking"이면 `HEALTH_RETRY_MS` 뒤 다시 보낸다. "other"면 멈춘다. 정리 함수에서 `abort()`와 타이머 해제.
3. **다시 시도** `reload`(이벤트 처리기): `res`, `wait`, `apiUp`, `holding`, `gaveUp`, `failed`를 되돌리고 `nonce`를 올린다.
4. **그리기**:
   - `res`가 있으면 `children`.
   - 실패나 포기면 기존 화면.
   - 그 밖에는 `<WakeWait label startedAt={wait?.startedAt ?? null} apiUp loaded={holding} />`. `startedAt`이 null이면 `WakeWait`는 카드와 같은 높이의 빈 자리와 비어 있는 낭독 영역만 그린다. 서버 렌더링과 첫 클라이언트 렌더링 모두 이 모습이라 하이드레이션 차이가 없다.

## 5. `WakeWait` 화면

```
┌──────────────────────────────────────────────────────────────────┐
│ ┌렉스 112px┐  서버를 깨우는 중이에요                         12초째 │
│ │ 숨쉬기    │  이 데모는 무료 서버에서 돌아가서, 15분 동안 방문이 없으면 잠들어요.│
│ └──────────┘  보통 30초에서 1분 걸려요.                              │
│ 음냐… 조금만 더… 0초 ▓▓▓▓▓▓░░░░░░░░░░░░░░░░░ 1분                  │
│ 렉스를 3번      ◌ 서버 깨우기   ○ 승인 대기함 불러오기               │
│ 흔들었어요      ─────────────────────────────────────────────────── │
│ 눌러도 빨라지지 기다리는 동안 읽을거리  ‹ ❚❚ ›                        │
│ 는 않아요       자동 검사는 세 번 · AI가 쓴 초안은 …                 │
│                36초 소개 영상 보기                                   │
└──────────────────────────────────────────────────────────────────┘
```

- `CARD` 하나. `sm` 이상 가로 배치(왼쪽 렉스 열 160px), 미만 세로. 빈 자리와 카드 모두 `min-h-[17rem]`.
- **낭독 영역**: `<div role="status" aria-live="polite">` 안에 제목, 안내 문장(`waitHint`), 단계 목록. 이 요소는 빈 자리 단계부터 같은 위치에 있고 내용만 채운다. 안내 문장은 60초 경계에서 한 번 바뀐다.
- **경과 시간**: `WakeWait` 안의 `now` state를 1초 `setInterval`로 갱신하고 `now - startedAt`으로 계산한다(탭이 백그라운드여도 돌아오면 맞는 값). `aria-hidden`.
- **시간 막대**: `aria-hidden`. 너비 `timeFraction` 퍼센트, `transition: width 1s linear`. 느림이면 너비 100%에 `.lw-stripes`(대각선 줄무늬가 흐름). 양 끝 "0초", "1분".
- **단계**: 아이콘(완료 `CircleCheck` 초록, 진행 `LoaderCircle animate-spin` 파랑, 남음 `Circle` 회색)과 숨은 글자("완료", "진행 중", "남음").
- **렉스 버튼**: 바깥 `span.lw-breathe`(2.8초, 0→-4px→0) 안에 `button`(`aria-label="렉스 흔들기, 기다리는 동안 하는 놀이"`), 그 안 `span.lw-wiggle`에 `key={taps}`를 줘 누를 때마다 애니메이션이 다시 시작된다(0.5초, 회전 0→-8→8→-4→0도). 잠꼬대 `snoreFor(taps)`와 "렉스를 n번 흔들었어요"는 `aria-live="polite"` 별도 영역, "눌러도 빨라지지는 않아요"는 고정 안내.
- **이야기 카드**: `paused = userPaused || hover || focusWithin`. 멈추지 않았으면 7초마다 `nextTip`. 이전, 다음을 누르면 넘기고 타이머를 다시 시작한다. 멈춤 버튼 `aria-pressed={userPaused}`, `aria-label="이야기 자동 넘김 멈추기"`. 사용자가 멈추면 마우스나 포커스가 떠나도 다시 재생하지 않는다. 카드 본문은 멈춘 동안만 `aria-live="polite"`(APG Carousel 방식). 머리말 "기다리는 동안 읽을거리", 카드 번호 "2/5".
- **영상 링크**: `/intro.mp4`, 새 탭, 숨은 글자 "(새 탭에서 열림)".
- 키프레임은 끝 값이 원래 위치라, 전역 움직임 줄이기 규칙이 길이를 0으로 만들면 제자리에 멈춘다. 회전 아이콘도 같은 규칙으로 멈춘다.

## 6. 높이 근거 (WW-05)

운영 화면(1280×800)에서 처음 방문 안내의 아래 끝이 약 506px이다. 대기 카드는 그 아래 약 24px 간격으로 시작하므로 제목은 약 560px, 시간 막대는 카드 위쪽에서 약 120px 아래인 680px쯤이다. 800px 안에 든다. 구현 뒤 Playwright로 실제 위치를 잰다.

## 7. 검증

- vitest: `wake.test.ts`(단계 조합, 막대 경계, 경과 문구, 안내 문장 경계, 잠꼬대 금지어, 카드 순환).
- 로컬 화면(Playwright): API 컨테이너를 멈춘 채 `/`를 열고 경과 시간이 오르는지, 1단계가 진행 중인지 찍는다. API를 다시 켜면 두 단계가 완료로 0.6초 보이고 목록이 뜨는지. 렉스를 눌러 문구와 횟수가 바뀌는지, 카드 멈춤과 넘기기, 61초 이후 안내 문장과 줄무늬, 1280×800에서 제목과 막대 위치, 375px 가로 스크롤 없음, 서버가 깨어 있을 때 대기 화면이 뜨지 않는지, 콘솔 오류 0건, 움직임 줄이기 에뮬레이션.
