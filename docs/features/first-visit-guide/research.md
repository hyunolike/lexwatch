# 처음 방문 안내 외부 레퍼런스 조사 (2026-11-23)

처음 들어온 사람이 무엇을 봐야 할지 알려 주는 방법을 찾았다. 출처를 직접 확인한 내용과 업체 블로그의 주장을 나눠 적는다.

## 1. 연구와 지침

| 출처 | 내용 | LexWatch에 쓰는 곳 |
|---|---|---|
| NN/g, Mobile Tutorials (2020) | 70명, iOS 앱 4개 원격 실험. 튜토리얼을 본 집단과 건너뛴 집단의 과제 성공률(91%, 94%)과 소요 시간(93.5초, 85.2초)에 유의한 차이가 없었고, 튜토리얼을 본 집단이 과제를 더 어렵게 느꼈다(7점 척도 4.92, 5.49, p=0.047). 단순한 앱에는 튜토리얼을 만들기 전에 다시 생각하고, 필요하면 맥락 도움말 같은 상호작용 방식을 쓰라고 권한다 ([링크](https://www.nngroup.com/articles/mobile-tutorials/)) | 여러 장짜리 강제 투어를 만들지 않는다 |
| NN/g, Onboarding Tutorials vs. Contextual Help (2023) | 튜토리얼은 사용자를 끊고 성과를 꼭 높이지 않으며 금방 잊힌다. 사용자가 부르는 도움말(pull)은 닫기 쉽고 다시 부르기 쉬워야 하고, 점진적으로 보여 주며, 외우게 하지 말고 해당 단계 옆에 둔다. ArcGIS는 튜토리얼을 사이드바에 두어 언제든 돌아가게 한다 ([링크](https://www.nngroup.com/articles/onboarding-tutorials/)) | 닫기와 다시 열기, 각 단계에 해당 화면 링크 |
| NN/g, Empty States in Complex Applications (2021) | 빈 상태로 시스템 상태(불러오는 중, 오류, 없음)를 알리고, 무엇이 어떻게 채워지는지 학습 단서를 주고, 핵심 작업으로 가는 길을 준다. 불러오는 중에 "없음"을 보여 주지 않는다 ([링크](https://www.nngroup.com/articles/empty-state-interface-design/)) | 서버를 깨우는 동안에도 안내가 보이게 한다. 이미 있는 빈 상태 문구는 유지 |
| Google PAIR, People + AI Guidebook: Mental Models | 제품이 할 수 있는 일과 못 하는 일의 기대가 어긋나면 실망과 오용, 이탈로 이어진다. "AI 마법" 문구를 피하고 알고리즘의 성격과 한계를 알리며, 온보딩 같은 제품 안의 순간을 설명에 쓴다 ([링크](https://pair.withgoogle.com/chapter/People%20+%20AI%20Guidebook%20-%20Mental%20Models.pdf), [코드랩](https://codelabs.developers.google.com/codelabs/pair-guidebook)) | 안내 첫 문단에 "AI 초안은 틀릴 수 있고 변호사가 승인해야 나간다"를 적는다 |
| W3C WAI-ARIA APG, Dialog (Modal) Pattern | 모달은 포커스를 가두고 닫히면 연 요소로 되돌린다. 배경을 막지 않는 것은 모달이 아니다 ([링크](https://www.w3.org/WAI/ARIA/apg/patterns/dialogmodal/), [MDN dialog role](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Roles/dialog_role)) | 안내를 대화상자로 띄우지 않고 페이지 안 영역으로 둔다. 다시 열면 안내 제목으로 포커스를 옮긴다 |

## 2. 업체 자료 (방향 참고만)

- 단계 수: Appcues는 3~5단계([링크](https://www.appcues.com/blog/product-tours-walkthroughs-ultimate-guide)), Chameleon은 5단계쯤이 한계라고 본다([링크](https://www.chameleon.io/blog/product-tours-still-work)). 둘 다 자사 데이터라 수치는 그대로 인용하지 않는다.
- 체크리스트: Chameleon은 Folk CRM이 활성화 단계를 강제 단계가 아니라 체크리스트 항목으로 보여 준 예를 든다. 항목마다 짧게 안내한다.
- 건너뛰기: UserGuiding은 이미 써 본 사람을 위해 건너뛸 수 있어야 한다고 본다([링크](https://userguiding.com/blog/product-walkthroughs)).

## 3. 정리

1. 강제 투어 대신 **닫을 수 있는 안내 영역 하나**를 승인 대기함 위에 둔다.
2. 안내는 세 가지만 담는다. 무엇을 하는 제품인지, AI가 할 수 없는 것, 둘러볼 순서 세 단계.
3. 각 단계는 해당 화면으로 가는 링크이고, 그 화면에 가면 완료로 표시한다.
4. 닫은 뒤에도 머리의 "둘러보기" 버튼으로 다시 연다.

확인하지 못한 것: 리걸테크 제품(Harvey, CoCounsel 등)의 처음 방문 안내는 공개 자료가 없었다. 데모나 포트폴리오 방문자를 대상으로 한 연구도 찾지 못했다.
