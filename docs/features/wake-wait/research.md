# 서버 깨우는 동안의 대기 화면 조사 (2026-11-24)

## 지금 상태 (운영 사이트에서 확인)

저장소를 비운 브라우저(1280×800)로 https://lexwatch-web.netlify.app/ 를 열고 0.3초, 1초, 4초, 15초에 화면을 찍었다.

- API(Render 무료)가 잠들어 있어 `/api/reviews` 응답이 23초 뒤에 왔다.
- 그동안 `WakeGate`가 "서버를 깨우는 중이에요" 화면을 보이지만, **정지 이미지 한 장과 "(0/24)"가 23초 내내 그대로**였다. 첫 요청이 Render가 깰 때까지 붙잡혀 있어 시도 횟수가 오르지 않는다. 화면이 멈춘 것처럼 보인다.
- 처음 방문 안내가 위에 있어 대기 화면이 첫 화면 아래쪽에 반쯤 걸쳐 보였다.

사용자가 "로딩 화면이 없다"고 느낀 이유는 대기 화면이 있어도 움직임과 진행 정보가 없고 눈에 잘 띄지 않아서다.

## 연구와 지침

| 출처 | 내용 | LexWatch에 쓰는 곳 |
|---|---|---|
| Nielsen, Response Times: The 3 Important Limits ([링크](https://www.nngroup.com/alertbox/response-times.html), [영상](https://www.nngroup.com/videos/3-response-time-limits-interaction-design/)) | 0.1초, 1초, 10초. 10초를 넘으면 주의가 흩어지므로 언제 끝날지 알려 주는 진행 표시가 필요하다 | 경과 시간과 예상 시간을 함께 보인다. 회전 아이콘만 두지 않는다 |
| Maister, The Psychology of Waiting Lines (1985) ([요약](https://uen.pressbooks.pub/servicesmgt/chapter/chapter-12-the-psychology-of-waiting/)) | 할 일 없는 기다림은 길게, 끝을 모르는 기다림은 더 길게, 이유를 모르는 기다림은 더 길게 느껴진다 | 왜 기다리는지(무료 서버가 잠듦), 얼마나 걸리는지(보통 30초에서 1분), 그동안 볼거리(제품 이야기, 영상)를 준다 |
| Buell & Norton, The Labor Illusion, Management Science 57(9), 2011 ([링크](https://econpapers.repec.org/RePEc:inm:ormnsc:v:57:y:2011:i:9:p:1564-1579)) | 시스템이 일하는 과정을 보여 주면(operational transparency) 기다림을 더 너그럽게 받아들이고 결과를 더 가치 있게 본다 | 실제로 일어나는 단계(화면 준비, API 서버 깨우기, 목록 불러오기)를 체크리스트로 보이고, 끝난 단계를 실제 응답으로 표시한다 |
| Harrison 외, Rethinking the Progress Bar, UIST 2007 ([링크](https://www.chrisharrison.net/index.php/Research/ProgressBars)) | 같은 시간이라도 진행 막대의 움직임에 따라 체감 시간이 달라진다. 끝에서 멈추는 것보다 처음에 느린 편이 덜 거슬린다 | 막대가 끝에서 멈춰 보이지 않게, 예상 시간을 넘기면 막대를 채운 채 "평소보다 오래 걸려요"로 바꾼다 |
| Google, Chrome Dino 제작기 ([링크](https://blog.google/products/chrome/chrome-dino/)) | 연결이 끊긴 막다른 순간을 덜 답답하게 하려고 오프라인 페이지에 숨은 게임을 넣었다 | 마스코트 렉스를 눌러 흔들어 깨우는 작은 놀이를 넣는다. 속도를 높인다고 말하지 않는다 |
| Slack, Customize loading messages ([링크](https://slack1447178842.zendesk.com/hc/en-us/articles/360000561106-Customize-loading-messages)) | 불러오는 동안 짧은 문구를 돌려 보여 준다 | 기다리는 동안 제품을 알려 주는 짧은 이야기 카드를 돌린다 |
| W3C, Understanding SC 2.2.2 Pause, Stop, Hide ([링크](https://www.w3.org/WAI/WCAG21/Understanding/pause-stop-hide)), Hidde de Vries의 해설 ([링크](https://hidde.blog/meeting-2-22-pause-stop-hide-with-prefers-reduced-motion/)) | 자동으로 시작해 5초 넘게 움직이거나 갱신되는 내용은 멈출 수 있어야 한다. `prefers-reduced-motion`만으로 충분한지는 의견이 갈린다 | 이야기 카드에 멈춤 버튼을 둔다. 마스코트 움직임은 `prefers-reduced-motion`에서 끈다(전역 규칙이 이미 있음) |

## 정리

1. 정지 화면을 **움직이는 대기 화면**으로 바꾼다. 경과 시간, 예상 시간, 실제 단계 체크리스트를 보인다.
2. 기다림을 채울 거리: 렉스 흔들어 깨우기, 제품 이야기 카드, 36초 소개 영상 링크.
3. 거짓 진행은 보이지 않는다. 막대는 일의 양이 아니라 **시간**을 나타내고, 단계 완료는 실제 응답으로만 표시한다.
4. 첫 화면 위쪽에서 보이도록 대기 화면을 낮고 넓게 만든다.

확인하지 못한 것: 리걸테크 제품의 대기 화면 사례, 포트폴리오 방문자가 무료 서버 대기에서 이탈하는 비율.
