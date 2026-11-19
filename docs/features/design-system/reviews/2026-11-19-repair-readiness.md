# 배포 화면 개선 준비 검토

- 검토: 독립 문맥의 Implementation Readiness Goldfish. 대상은 tasks.md의 화면 진단 후 개선 계획과 spec.md 7절, design.md의 개선 결정.
- Major 1건: 최종 CSS 검사의 필수 선택자, 선언과 기대값이 정해지지 않아 이름만 있어도 통과할 위험.
- 조치: design.md에 검사 계약을 추가했다. 실제 토큰 값 대조와 필수 선언, 이전 규칙 혼재 실패, 분할/HTTP 오류 fixture를 명시했다.
- API와 권한 정책, 초안 없음/지적 없음 구분에서는 추가 Major/Blocker 없음.
- 검토자는 원문 지침/표준, 세 문서, package.json, api.ts, 토큰, 페이지/상세/문서 화면과 직접 관련 공통 부품을 읽었다.
