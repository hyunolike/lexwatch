# 배포 런북 (Supabase + Render + Vercel + GitHub Actions)

저장소에서 준비된 것: `backend/Dockerfile`, `ai/Dockerfile`, `web/Dockerfile`, `docker-compose.yml`, `render.yaml`, `ai/vercel.json`, `.github/workflows/{ci,collect,reset-demo}.yml`.
아래 1~5단계는 계정이 필요한 작업이다. 2026-11-16에 한 번 실행했고 결과는 아래 배포 기록에 있다. 각 단계의 "확인" 항목이 통과해야 다음으로 넘어간다.
**검증 표기**: `[검증됨]`은 로컬에서 실제로 돌려 본 것, `[미검증]`은 외부 서비스에서만 확인할 수 있는 것이다. 단계 제목의 `[미검증]`은 작성 당시 표기이고, 실제로 확인한 것은 배포 기록에 적는다.

## 배포 기록 (2026-11-19) — 법령 실수집 시작

국가법령정보센터 OC를 받아 실수집을 켰다.

| 항목 | 한 일과 확인한 것 |
|---|---|
| OPEN API 신청 | open.law.go.kr에서 신청 1건, 활용상태 "연결", 승인. 도메인은 "도메인 없음". 신청하면 자동 승인된다(활용목적이 비면 취소될 수 있다) |
| OC 값 | 마이페이지 → API인증키관리에서 정한 값이다. 가입 이메일의 `@` 앞부분이 아니어도 된다 |
| `LAW_OC` | Render(API로 `PUT /v1/services/<id>/env-vars/LAW_OC`)와 Vercel ai(`vercel env add LAW_OC production --sensitive`)에 넣었다. Vercel ai에는 `AI_TOKEN`이 먼저 걸려 있었다 |
| 실응답 녹화 | 로컬에서 `scripts/record_lawapi.py`로 녹화해 `fixtures/lawapi/recorded/`에 두었다. 파서가 추정한 형태와 달라 고쳤다(#4, 자세한 차이는 `docs/notes/2026-11-16-spike.md`) |
| 배포 | #4 머지(`468a7da`). Vercel ai는 push로 자동 배포됐다. Render는 `POST /v1/services/<id>/deploys`로 배포해 live, `/health` 200 |
| 수집 | `collect` 수동 실행 성공, `{"created":3,"skipped":0}`. Render에서 호출해도 IP 검증에 걸리지 않았다 |

수집된 실행 3건:

| 법령 | 근거 수 | 신구조문 | 분류 |
|---|---|---|---|
| 약사법 시행규칙 | 98 | `ok` | 세 그룹 모두 관련 없음 |
| 특별검사 임명 등에 관한 법률(제정) | 26 | `absent` | 세 그룹 모두 관련 없음 |
| 치료감호 등에 관한 법률 시행규칙 | 55 | `absent` | 세 그룹 모두 관련 없음 |

배포하면서 알게 된 것:
- 같은 날 오전 예약 `collect`는 Render에 `LAW_OC`가 없어 502로 실패했다. 런북의 설명대로 법제처 호출 실패다.
- Render 배포 API의 `commitId`는 전체 SHA여야 한다. 짧은 SHA를 넣으면 `does not have a commit`으로 거절된다.
- Vercel ai에 `NVIDIA_API_KEY`가 없어 분류가 `similarity-fallback`으로 돌았다(신뢰도 0.10~0.21). 이번 세 법령은 실제로도 세 그룹과 거리가 멀지만, 키를 넣기 전에는 실제 개정에서 초안이 만들어지지 않는다.
- 아직 확인하지 않은 것: 근거 본문에 항 내용까지 들어갔는지(실행 상세 API는 근거 개수만 보여 준다. 녹화본 테스트로만 확인), 실제 개정으로 초안과 크리틱까지 도는 한 바퀴(`NVIDIA_API_KEY` 필요).

## 배포 기록 (2026-11-16)

아래 1~5단계를 CLI(`supabase`, `vercel`, `render ea blueprints`, `gh`)로 실행했다. 확인한 것과 남은 것을 나눠 적는다.

| 구성 | 주소 | 확인한 것 |
|---|---|---|
| Supabase | 프로젝트 `lexwatch`, ap-southeast-1, 세션 풀러 | 첫 기동에서 마이그레이션이 적용되고 재현 검토가 시드됐다 |
| Vercel ai | https://lexwatch-ai.vercel.app (sin1, Fluid Compute) | `/health` 200, `/classify {}` 422 |
| Render api | https://lexwatch-api-qinh.onrender.com (singapore, free) | `/health` 200, 재현 검토 목록, 토큰 없는 `/jobs/reset-demo` 401, 토큰 있는 호출 200 |
| Vercel web | https://lexwatch-web.vercel.app (sin1) | CORS 사전 요청 204, 승인 대기함 표시, 재현 검토 승인 뒤 알림함 도착 |
| GitHub | 시크릿 `API_BASE_URL`, `JOB_TOKEN` 설정 | — |

배포하면서 고친 것과 알게 된 것:
- Render 서비스 이름은 블루프린트가 붙인 접미사 때문에 `lexwatch-api-qinh`다.
- `sync: false` 항목은 값이 비면 블루프린트 승인이 거부된다. 그래서 `LAW_OC`는 블루프린트에서 뺐고, 키를 받으면 따로 넣는다(아래 3단계).
- Vercel ai의 rewrite를 걷어냈다(2단계 참고).
- 예약 워크플로(`collect`, `reset-demo`)는 저장소의 **기본 브랜치**에서만 돈다. 처음 push한 `feat/mvp`가 기본 브랜치로 잡혀 있었고, `main`에 합친 뒤 기본 브랜치를 `main`으로 바꿨다. `reset-demo` 수동 실행으로 확인했다.
- CLI로 만든 Vercel 프로젝트는 Root Directory가 비어 있어서, 저장소 루트에서 빌드하는 Git 연동 배포가 실패했다(web: `NEXT_NO_VERSION`, ai: `FASTAPI_ENTRYPOINT_NOT_FOUND`). 두 프로젝트에 `web`, `ai`를 넣은 뒤 Git 연동 배포가 성공한다. 이제 `main`에 push하면 Vercel 두 곳은 자동으로 배포된다.
- Render 배포 브랜치는 `main`이다.
- Render 자동 배포가 push에 반응하지 않는다(저장소를 URL로 연결해 GitHub 앱 웹훅이 없다). 새 커밋은 `POST /v1/services/<id>/deploys`에 `commitId`(전체 SHA)를 넣거나 대시보드에서 배포한다. (2026-11-19부터 CI가 배포한다. 위 "자동 배포" 절)
- AI 토큰: 백엔드를 먼저 새 코드로 올리고(이전 ai는 헤더를 무시한다) 그다음 Vercel ai에 `AI_TOKEN`, `AI_REQUIRE_TOKEN=true`를 넣고 재배포했다. 토큰 없는 호출 401, `/docs`·`/openapi.json` 404, 백엔드 경유 채팅 200을 확인했다.
- 아직 확인하지 않은 것: 실제 `/analyze` 한 건의 응답 시간(키 필요), Render 메모리 추이, `CLIENT_IP_HEADER` 값(키가 없으면 채팅이 할당량을 쓰지 않아 실험할 수 없다), UptimeRobot.

## 자동 배포

`main`에 push(머지)하면 아래 순서로 배포된다.

| 대상 | 방식 | 테스트 통과를 기다리는가 |
|---|---|---|
| Vercel web, ai | Vercel Git 연동(Root Directory `web`, `ai`) | 아니요. push 즉시 빌드 |
| Render API | `ci.yml`의 `deploy-api` 잡이 Render CLI로 배포한다 | 예. `backend`, `ai`, `openapi-drift`, `web`, `docker` 다섯 잡이 모두 통과해야 실행 |

`deploy-api`는 Render CLI(버전 2.36.0, 체크섬 고정)를 받아 `render deploys create <서비스 ID> --commit <커밋> --wait`로 그 커밋을 배포하고 끝날 때까지 기다린다. 배포가 실패하면 잡도 실패한다. 이어서 공개 주소의 `/health` 응답 `commit`(Render가 넣는 `RENDER_GIT_COMMIT`)이 그 커밋인지 확인한다. 동시에 두 배포가 돌지 않도록 `concurrency: deploy-api`로 줄을 세운다. Render의 자체 자동 배포(`autoDeploy`)는 꺼서 이중 배포를 막는다(`render.yaml`, 서비스 설정 둘 다. 서비스 설정은 2026-11-19에 끔).

준비(한 번):
1. Render 대시보드 → Account Settings → **API Keys**에서 키를 만든다. 이 키는 워크스페이스 전체 권한(다른 서비스, 환경변수의 DB 비밀번호 읽기)을 가지므로 비밀로 다루고, 유출이 의심되면 바로 지우고 새로 만든다.
2. 터미널에서 `gh secret set RENDER_API_KEY`를 실행하고 키를 붙여 넣는다(명령줄 인자로 넘기지 않는다).
3. `API_BASE_URL` 시크릿은 예약 작업용으로 이미 있다.

시크릿이 없으면 `deploy-api`가 실패하고 그 사실을 로그에 남긴다. 수동 배포는 `render deploys create srv-dbt9gtgae00c73a31iag --commit <전체 SHA> --wait` 또는 대시보드의 Manual Deploy.

남은 빈틈: Vercel은 테스트 결과를 기다리지 않는다. 테스트가 실패한 커밋도 웹과 ai에는 배포될 수 있다.

## 구성과 순서

```
GitHub Actions (collect 07:00 KST, reset-demo 04:00 KST)  --X-Job-Token-->  Render (backend, Docker, free 512MB)
Vercel web (Next.js)  --HTTPS-->  Render  --HTTPS-->  Vercel ai (FastAPI, Python serverless)
Render  --Postgres(세션 풀러)-->  Supabase (pgvector)
```

주소가 서로를 가리키므로 순서는 Supabase → Vercel ai → Render → Vercel web → Render의 `Web__Origins` 갱신 → GitHub 시크릿 → UptimeRobot이다.

## 0. 환경변수 전체 표

| 이름 | 어디서 | 용도 | 비고 |
|---|---|---|---|
| `DATABASE_URL` | Render | DB 연결. `Host=...;...` 형식 또는 `postgresql://` URI 모두 받는다 | 비밀. 코드가 URI를 Npgsql 형식으로 바꾼다 (`ConnectionStringNormalizer`) |
| `AI_BASE_URL` | Render | Vercel ai 주소 | 예: `https://lexwatch-ai.vercel.app` |
| `JOB_TOKEN` | Render, GitHub 시크릿 | `/jobs/*` 인증, 채팅 IP 해시 솔트 | 비밀. 비어 있으면 잡과 채팅이 닫힌다 |
| `AI_TOKEN` | Render, Vercel ai (같은 값) | Render → ai 호출의 `X-Ai-Token` 공유 토큰 | 비밀. 아래 "AI_TOKEN" 절. **`NVIDIA_API_KEY`·`LAW_OC`를 ai에 넣기 전에 먼저 설정** |
| `AI_REQUIRE_TOKEN` | Vercel ai | `true`면 `AI_TOKEN`이 비었을 때 ai가 503으로 닫힌다 | 배포에서는 `true` 권장. 로컬·테스트는 비워 둔다 |
| `CLIENT_IP_HEADER` | Render | 채팅 할당량이 클라이언트 IP를 읽을 헤더 하나(예: `True-Client-IP`) | 기본 비움. 아래 "클라이언트 IP" 절 먼저 읽을 것 |
| `LAW_OC` | Render, Vercel ai | 법제처 OPEN API 인증값 | 비밀. 없으면 수집이 실패하고 재현 실행만 동작. ai의 에이전트 도구(`ai/app/lawapi.py`)도 법제처를 직접 부르므로 두 곳 모두 넣는다. 2026-11-19 설정 |
| `Web__Origins` | Render | CORS 허용 오리진(쉼표 구분) | Vercel web 주소. 기본값은 `http://localhost:3000` |
| `MIGRATE_ON_STARTUP` | Render | 시작 시 EF 마이그레이션 적용 | `true` (render.yaml이 설정) |
| `DEMO_SEED_ON_STARTUP` | Render | 재현 실행이 하나도 없으면 시작 시 시드 | `true` (render.yaml이 설정) |
| `TRUST_FORWARDED_FOR` | Render | 채팅 할당량의 IP 출처 | 기본 `false`. 아래 "TRUST_FORWARDED_FOR" 절 먼저 읽을 것 |
| `PORT` | Render가 주입 | 수신 포트 | 없으면 8080 |
| `NVIDIA_API_KEY` | Vercel ai | NIM 호출 키 | 비밀. 없으면 mock LLM과 lexical 검색으로 동작한다 |
| `NIM_BASE_URL`, `NIM_MODEL`, `EMBED_MODEL`, `RERANK_MODEL`, `RERANK_URL` | Vercel ai | 모델·엔드포인트 | 기본값이 `.env.example`과 같다. 바꿀 때만 설정 |
| `LLM_PROVIDER`, `RETRIEVER_PROVIDER` | Vercel ai | `auto`(기본)/mock/lexical 강제 | 키가 있어도 mock으로 데모하려면 지정 |
| `NEXT_PUBLIC_API_BASE_URL` | Vercel web | 브라우저가 부를 백엔드 주소 | **빌드 때 번들에 박힌다.** 바꾸면 재배포 |

비밀은 저장소에 두지 않는다. `render.yaml`은 비밀을 `sync: false`로 두고 값을 대시보드에서 넣게 한다.

## 마이그레이션 방식

앱이 시작할 때 `MIGRATE_ON_STARTUP=true`이면 `Database.MigrateAsync()`를 실행한다. docker-compose와 Render가 같은 방식이다.
Render는 배포 중 헬스체크가 통과할 때까지 이전 인스턴스와 새 인스턴스가 잠시 겹칠 수 있다. 동시 시작의 안전은 EF Core `Migrate`의 Postgres advisory lock이 맡는다(Supabase 세션 풀러에서 동작 `[미검증]`). 또 그 사이 이전 인스턴스가 새 스키마를 보므로 마이그레이션은 하위 호환이어야 한다(컬럼 삭제·이름 변경은 두 단계로 나눈다).
마이그레이션은 `CREATE EXTENSION IF NOT EXISTS vector`도 포함한다(`Initial`) — 그래도 아래 1단계에서 먼저 켜 두면 권한 문제를 시작 전에 알 수 있다.

## 1. Supabase `[미검증]`

1. https://supabase.com 에서 프로젝트 생성. 리전은 Render 서비스와 가까운 곳(예: Singapore/Oregon 중 Render 리전과 맞춘다). DB 비밀번호를 기록한다.
2. SQL Editor에서 실행: `CREATE EXTENSION IF NOT EXISTS vector;`
3. 연결 문자열: Project Settings → Database → **Connection string → Session pooler**(포트 5432)를 쓴다.
   - 직접 연결(`db.<ref>.supabase.co`)은 IPv6 전용이라 Render에서 닿지 않을 수 있다.
   - Transaction pooler(포트 6543)는 쓰지 않는다. Npgsql의 준비된 문장과 맞지 않는다.
   - 사용자 이름은 `postgres.<project-ref>` 형식이다.
4. 예: `postgresql://postgres.<ref>:<비밀번호>@aws-0-<region>.pooler.supabase.com:5432/postgres?sslmode=require` — 이 값이 Render의 `DATABASE_URL`이다. 비밀번호에 특수문자가 있으면 URL 인코딩한다.
5. 마이그레이션은 Render 첫 기동 때 적용된다(3단계). 별도 수동 작업은 없다.

확인: 3단계 뒤 Table Editor에 `LawChanges`, `WorkflowRuns`, `StepRuns` 등이 보이고 `__EFMigrationsHistory`에 4개 행(`Initial`, `CollectLog`, `LawChangeMstNotificationCascade`, `EvidenceChangeMarks`)이 있다.
주의: `sslmode=require`는 Npgsql 10에서 암호화만 요구하고 인증서는 검증하지 않는다.

## 2. Vercel — `ai` `[미검증]`

1. Vercel → Add New Project → 이 저장소 선택. **Root Directory = `ai`**. Framework Preset = Other.
2. `ai/vercel.json`이 `api/index.py`의 `maxDuration: 150`, `fluid: true`, `regions: ["sin1"]`을 정한다. `api/index.py`는 `app.main:app`을 가져온다.
   **rewrite를 두지 않는다.** Vercel이 FastAPI를 감지해 모든 경로를 앱으로 보내는데, `/(.*) → /api/index` rewrite를 걸면 앱이 받는 경로가 `/api/index`로 바뀌어 모든 요청이 404가 된다(2026-11-16 실제 배포에서 겪었다).
3. 의존성은 `ai/pyproject.toml`의 `dependencies`만 설치된다. 선택 그룹 `guardrails`(`nemoguardrails`)는 설치되지 않는다 — 번들에 넣지 않는다(ai/README.md 참고). `tests/`, `.venv/` 등은 `excludeFiles`로 뺀다.
4. 환경변수: `AI_TOKEN`(Render와 같은 값), `AI_REQUIRE_TOKEN=true`, `NVIDIA_API_KEY`(없어도 됨), `LAW_OC`(ai 서비스가 법제처를 직접 부르는 경로가 있을 때만), 필요 시 `LLM_PROVIDER`.
5. 배포 후 주소(예: `https://lexwatch-ai.vercel.app`)를 기록한다.

**함수 최대 실행 시간 (중요, `[미검증]`)**: 백엔드는 `/analyze`를 최대 130초까지 기다린다(`AiGatewayOptions.Analyze`). 그래서 `maxDuration`을 150으로 두었다.
Vercel 문서 기준(작성 시점 기억에 의존, 직접 확인하지 못했다): Hobby 플랜은 Fluid Compute가 켜져 있으면 최대 300초, 꺼져 있으면 60초다.
**프로젝트에서 Fluid Compute가 꺼져 있으면 150초 설정은 배포 시 거부되거나 60초로 잘려 `/analyze`가 실패한다.**
대응: (a) Settings → Functions에서 Fluid Compute를 켠다, (b) 켤 수 없으면 `maxDuration`을 허용 한도로 낮추고 `AiGatewayOptions.Analyze`와 `AddAiGateway`의 `Timeout`을 그보다 짧게 맞춘 뒤, 분석이 그 안에 끝나는지 실제 호출로 확인한다(키 없는 mock은 즉시 끝난다).
어느 쪽이든 아래 확인의 `/analyze` 호출이 통과해야 "동작한다"고 말할 수 있다.

확인:
```bash
curl -s https://<ai>.vercel.app/health            # {"status":"ok"} (토큰 없이 열린 유일한 경로)
curl -s -o /dev/null -w '%{http_code}\n' -X POST https://<ai>.vercel.app/classify -H 'content-type: application/json' -d '{}'   # 401 (토큰 없음)
curl -s -o /dev/null -w '%{http_code}\n' https://<ai>.vercel.app/docs        # 404 (토큰을 건 배포는 문서를 열지 않는다)
curl -s -X POST https://<ai>.vercel.app/classify -H "X-Ai-Token: $AI_TOKEN" -H 'content-type: application/json' -d '{}'   # 422 (스키마 검증이 동작)
```

### AI_TOKEN

ai 서비스는 NVIDIA 크레딧과 `LAW_OC`를 쓰므로 공개 주소를 누구나 부르면 채팅 할당량을 우회하고 키를 소진할 수 있다. 그래서 `.NET API`만 부르게 한다.
- ai: `AI_TOKEN`이 있으면 `/health`를 뺀 모든 경로가 같은 값의 `X-Ai-Token` 헤더를 요구한다(상수 시간 비교, 틀리면 401). `/docs`, `/redoc`, `/openapi.json`은 404다.
- ai: `AI_TOKEN`이 비어 있으면 검사하지 않는다(로컬·테스트용). `AI_REQUIRE_TOKEN=true`이면 대신 503으로 닫힌다.
- Render: `AI_TOKEN`이 있으면 AI 호출마다 `X-Ai-Token`을 붙인다(`AiTokenHandler`). 계약 생성(`scripts/gen-ai-client.sh`)은 `app.openapi()`를 직접 부르므로 영향이 없다.
- 값: `openssl rand -hex 32`. Render와 Vercel ai에 **같은 값**을 넣고, 순서는 Vercel ai 재배포와 Render 재시작을 연달아 한다. 사이에 들어온 AI 호출은 401이고, 백엔드는 이를 재시도하지 않고 단계 실패(응답 본문의 `AI_TOKEN_INVALID`)로 남긴다 — 실행 이력에서 관리자가 다시 실행한다. 수집이 없는 시간(07:00 KST 근처를 피해서)에 바꾼다. 로그에는 남기지 않는다.

실제 `/analyze` 한 건은 요청 본문이 크므로 `fixtures/` 아래 기록이나 `ai/openapi.json`의 `AnalyzeRequest` 스키마로 만들어 호출하고, 응답 시간을 기록한다.

## 3. Render — 백엔드 `[미검증]`

1. Render → New → Blueprint → 이 저장소 선택. `render.yaml`이 서비스 `lexwatch-api`(Docker, free, 헬스체크 `/health`, 빌드 컨텍스트는 저장소 루트)를 만든다.
2. `sync: false` 항목 입력: `DATABASE_URL`(1단계), `AI_BASE_URL`(2단계), `JOB_TOKEN`(임의의 긴 문자열, 예: `openssl rand -hex 32`), `LAW_OC`, `Web__Origins`(4단계 주소를 알 때까지 임시로 `http://localhost:3000`).
3. 첫 배포 로그에서 확인: `MigrateAsync` 적용, 데모 시드 실행, `Now listening on: http://0.0.0.0:<PORT>`.
4. **메모리 512MB 확인**: Render 대시보드 Metrics의 Memory가 512MB 아래에서 안정적인지 본다. 이미지는 힙을 256MB로 묶었다(`DOTNET_GCHeapHardLimit=10000000`, 16진수=256MiB), `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, 워크스테이션 GC. OOM으로 재시작하면 `DOTNET_GCHeapHardLimit`을 `C000000`(192MiB)로 낮추고 이 문서에 기록한다.
   로컬(docker stats, 재현 승인 1건 뒤 유휴)에서는 api 78~93 MiB였다. 부하나 실제 분석 중 값은 측정하지 않았다.
5. 무료 플랜은 15분 무요청 시 잠들고 깨울 때 1분쯤 걸린다. 워크플로 `collect.yml`/`reset-demo.yml`이 먼저 `/health`를 재시도로 깨운다.

확인:
```bash
curl -s https://<render>.onrender.com/health                       # {"status":"ok"}
curl -s -H 'X-Demo-Role: lawyer' https://<render>.onrender.com/api/reviews   # 시드된 재현 검토 목록
```

### TRUST_FORWARDED_FOR

채팅 할당량(`POST /api/reviews/{id}/chat`)은 IP 해시로 센다. 기본은 연결의 원격 IP인데 Render 뒤에서는 프록시 IP 하나가 보이므로 모든 사용자가 한 버킷을 공유한다.
`TRUST_FORWARDED_FOR=true`로 바꾸면 `X-Forwarded-For`의 **마지막** 항목을 쓴다. 이것이 실제 클라이언트 IP일 때만 안전하다.
**켜기 전에 확인할 것** `[미검증]`: Render 프록시가 클라이언트 요청의 `X-Forwarded-For` 뒤에 실제 클라이언트 IP를 덧붙이는지(마지막 항목이 프록시가 본 연결 IP인지). 임시 엔드포인트나 로그로 서로 다른 두 IP에서 호출해 마지막 항목이 각각 다른지 확인한다.
확인하기 전까지는 `false`로 둔다 — 이때의 부작용은 데모 사용자 전체가 일일 10회 한도를 나눠 쓴다는 것뿐이다.
주의: Render 앞에 Cloudflare가 있다(응답 헤더 `server: cloudflare`, `cf-ray`). 그래서 XFF의 마지막 항목이 클라이언트가 아니라 프록시 홉일 수 있다. 이 경우 아래 `CLIENT_IP_HEADER`를 쓴다.

### 클라이언트 IP (`CLIENT_IP_HEADER`)

`CLIENT_IP_HEADER`에 헤더 이름 하나(예: `True-Client-IP` 또는 `CF-Connecting-IP`)를 넣으면 할당량은 그 헤더만 읽고, `TRUST_FORWARDED_FOR`는 무시된다.
값이 IP 주소 하나로 읽히지 않으면(빈 값, 쉼표 목록, 문자열) 연결의 원격 IP로 내려간다. 비어 있으면 위 `TRUST_FORWARDED_FOR`/원격 IP 규칙 그대로다.
**넣기 전에 확인할 것** `[미검증]`: Render(Cloudflare)가 그 헤더를 **덮어써서** 넘기는지(클라이언트가 보낸 같은 이름의 헤더를 지우는지). 서로 다른 두 네트워크에서 호출해 헤더 값이 달라지는지, 클라이언트가 임의 값을 보내면 그대로 통과하는지를 본다. 값 자체는 로그에 남기지 않는다(헤더가 있었는지, IP로 읽혔는지만 본다). 덮어쓰지 않는 헤더를 고르면 누구나 할당량을 우회할 수 있다.
`render.yaml`에는 넣지 않았다. 확인한 뒤 대시보드에서 넣는다.

## 4. Vercel — `web` `[미검증]`

1. Add New Project → 같은 저장소 → **Root Directory = `web`**. Framework = Next.js (자동).
2. 환경변수 `NEXT_PUBLIC_API_BASE_URL` = Render 주소(`https://<render>.onrender.com`, 끝 슬래시 없음). **빌드 때 박히므로 값을 바꾸면 재배포해야 한다.**
3. 배포 후 주소를 Render의 `Web__Origins`에 넣고(여러 개면 쉼표) 저장하면 Render가 재시작한다. CORS는 `X-Demo-Role`, `Content-Type` 헤더와 GET/POST만 허용한다.
4. `web/vercel.json`은 리전(`sin1`)만 정한다. 빌드 명령은 기본값을 쓴다.

확인: 브라우저로 주소를 열면 수신함이 보이고, 네트워크 탭에서 Render로 가는 요청이 CORS 오류 없이 200이다. 서버가 잠들어 있으면 화면이 "깨우는 중" 상태로 재시도한다.

## 5. UptimeRobot, GitHub 시크릿, 수집 한 바퀴 `[미검증]`

### UptimeRobot (무료)
- New monitor → HTTP(s) → URL `https://<render>.onrender.com/health` → 간격 **10분**(무료 플랜 최소는 5분이므로 10분 설정 가능).
- 이것은 **API가 잠들지 않게 하는 핑**일 뿐이다. 수집·초기화 작업은 GitHub Actions가 한다. 모니터가 초록이어도 작업이 돌았다는 뜻은 아니다.
- `/health`는 추적과 로그에서 제외돼 있어 로그가 불어나지 않는다.
- 10분 핑은 무료 서비스를 사실상 항상 깨워 둔다. Render 무료 플랜의 월 750 인스턴스 시간(31일 = 744시간)을 거의 다 쓰므로, 같은 계정에 다른 무료 서비스가 있으면 한도를 함께 확인한다.

### GitHub Actions 시크릿
- 저장소 Settings → Secrets and variables → Actions → New repository secret:
  - `API_BASE_URL` = `https://<render>.onrender.com` (끝 슬래시 없음)
  - `JOB_TOKEN` = Render에 넣은 값과 **동일**
- CI(`ci.yml`)는 시크릿을 쓰지 않는다. 시크릿은 `collect.yml`, `reset-demo.yml`만 쓴다.

### 수집 수동 실행
- Actions 탭 → `collect` → Run workflow. 단계: 깨우기(`/health`, 최대 약 3분 재시도) → `POST /jobs/collect` → 10분간 1분마다 `/health`(처리 중 잠들지 않게).
- 확인: 응답이 `{"created":N,"skipped":M}`이고(`LAW_OC`가 유효해야 한다. 502면 법제처 호출 실패), 웹 수신함에 새 실행이 생기며 `/runs/<id>`의 타임라인에서 단계가 진행된다.
  이후 분석 단계는 Vercel ai를 부르므로 `NVIDIA_API_KEY` 없이도 mock으로 완료된다.
- `reset-demo`도 한 번 수동 실행해 `{"reset":true}`를 확인한다.

## 로컬 compose 메모

`docker compose up --build` → http://localhost:3000. 키 없이 mock/lexical로 뜨고 재현 실행이 시드된다. 셸에 `AI_TOKEN`이 있으면 ai와 api에 같은 값으로 넘어간다.
compose의 `JOB_TOKEN` 기본값(`local-dev-job-token`)은 공개된 값이다. compose를 공개 주소로 열지 않는다(포트는 `127.0.0.1`에만 묶여 있다). Docker 20.10 + Compose v2.10 에서는 `export DOCKER_API_VERSION=1.41`이 필요할 수 있다.
첫 기동 로그에 `libgssapi_krb5.so.2` 경고와, 마이그레이션 이력 테이블이 아직 없어서 나는 `Failed executing DbCommand ... __EFMigrationsHistory` 오류 한 줄이 보인다. 둘 다 정상이다.
(작성 환경에서는 Docker Desktop의 자격 증명 도우미가 Docker Hub 이미지 조회에서 멈춰, 빈 `DOCKER_CONFIG` 디렉터리를 지정해 우회했다. 이 저장소와 무관한 로컬 문제다.)

## 추적과 로그

- 백엔드: Serilog 한 줄 JSON 로그(`@t`, `@m`, `trace_id`, `span_id`, 워커 단계는 `run_id`/`step`/`attempt`), OpenTelemetry span은 콘솔 출력. `WorkflowRun.TraceId`는 실행 생성 시 W3C trace id로 만들고 모든 단계 span이 이 trace를 잇는다. AI 서비스 호출에는 `traceparent`가 전파된다.
- AI: `traceparent`를 읽어 접근 로그에 `trace_id=`를 남긴다(메서드, 경로, 상태, 소요 시간만).
- 남기지 않는 것: 클라이언트 IP, 쿼리 문자열, UA, 채팅 질문, 비밀. 법제처 호출(쿼리에 OC)은 추적에서 제외한다. 서버 span의 IP·쿼리 태그는 내보내기 전에 지운다.
- Render는 stdout을, Vercel은 함수 로그를 수집한다. 한 실행을 따라가려면 `/runs/<id>`의 `traceId`로 양쪽 로그를 검색한다.
