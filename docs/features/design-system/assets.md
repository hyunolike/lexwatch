# 마스코트 "렉스" 에셋 생성 기록

2026-11-19, Codex CLI 0.170.0 `codex exec`의 내장 이미지 생성(`image_generation` 기능)으로 만들었다. 결과는 1024x1024 PNG이고, 투명 배경 WebP로 줄여 `web/public/mascot/`에 둔다.

## 1. 기본형 (`lex-master`)

```
codex exec --skip-git-repo-check -s workspace-write -C <out> "<아래 프롬프트>"
```

> Use your built-in image generation tool to create ONE image and save it as lex-master.png (1024x1024 PNG).
> Subject: the mascot of 'LexWatch', a Korean law-firm tool that watches statute amendments every morning. A cute small round owl character named Lex, rendered as a soft 3D clay / vinyl-toy style figure (smooth matte material, gentle studio lighting, subtle soft shadow under the feet).
> Design: chubby round body, big friendly eyes with small round glasses, tiny beak, short wings. Wears a deep navy blazer (#1F2A44) with a small gold pin shaped like scales of justice, a white shirt collar. Feather color warm cream and light grey-brown. Accent color soft violet (#6D5BD0) used only in a small bookmark ribbon. Holding a small paper document under one wing.
> Pose: standing, facing front, three-quarter body, neutral friendly smile.
> Background: plain pure white, no text, no logo, no extra objects. Centered with generous margin.
> Style: premium, calm, trustworthy, not childish; suitable for professional software used by lawyers.

## 2. 상황별 포즈

기본형을 참조 이미지로 넘긴다. `-i`는 파일을 여러 개 받으므로 프롬프트 뒤에 둔다.

```
codex exec --skip-git-repo-check -s workspace-write -C <out> "<공통 문장> Pose: <포즈> Save it as <이름>.png" -i lex-master.png
```

공통 문장:

> Use your built-in image generation tool with the attached image as the exact character reference: same owl named Lex, same 3D clay vinyl-toy style, same navy blazer with gold scales-of-justice pin, round glasses, cream and grey-brown feathers, same lighting and material. Keep the character identical; only change the pose and props described. Plain pure white background, no text, no letters, no logo. Centered, generous margin, 1024x1024 PNG.

| 파일 | 쓰는 곳 | 포즈 |
|---|---|---|
| `lex-avatar` | 헤더 로고, 파비콘 | Only Lex's head and upper shoulders, front-facing, friendly smile, tightly framed like an app icon avatar inside an invisible circle, filling about 80% of the frame. |
| `lex-empty-queue` | 승인 대기함이 비었을 때 | Lex sits relaxed on a small neat stack of closed documents, holding a small white coffee mug with both wings, eyes gently closed in a content smile, a small check-mark badge on the top document. |
| `lex-empty-inbox` | 알림함이 비었을 때 | Lex stands next to a small rounded navy mailbox, peeking inside it with a curious head tilt, one wing on the open lid; the mailbox is empty. |
| `lex-waking` | 서버를 깨우는 동안 | Lex is just waking up: sleepy half-closed eyes, one wing rubbing an eye, glasses slightly tilted, holding a small round alarm clock in the other wing, a small nightcap in soft violet on the head. |
| `lex-error` | 불러오기 실패 | Lex looks slightly worried but calm, holding a small magnifying glass up to a document that has a torn corner, one eyebrow feather raised. |

## 3. 후처리

`scripts/process_mascot.py <원본 폴더>`가 가장자리에서 이어진 흰 영역을 투명하게 바꾸고 WebP로 저장한다. 같은 프롬프트로 다시 생성하면 모양이 달라지므로, 교체할 때는 모든 포즈를 같은 기본형에서 다시 만든다.
