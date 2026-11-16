# LexWatch backend

## 테스트 실행

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
dotnet test backend/LexWatch.sln
```

테스트는 Testcontainers로 `pgvector/pgvector:pg16` 컨테이너를 띄우므로 Docker가 필요하다.

### 구형 Docker 데몬 (API 1.41, Docker 20.10 등)

`client version 1.44 is too new` 오류가 나면 실행 전에 환경변수로 API 버전을 낮춘다.
코드에는 고정하지 않는다 (Docker Engine 29 이상은 API 1.44 미만을 거부한다).

```bash
export DOCKER_API_VERSION=1.41
dotnet test backend/LexWatch.sln
```

## 데모 시드

- `POST /jobs/reset-demo` (`X-Job-Token`): 승인·알림·채팅 할당량·재현 실행을 지우고 `fixtures/demo/replays/*.json`을 다시 넣는다. 실제 수집 실행은 지우지 않고 결정된 그룹만 대기 상태로 되돌린다. AI 서비스 없이도 동작한다.
- `DEMO_SEED_ON_STARTUP=true`이면 재현 실행이 하나도 없을 때 기동 시 한 번 넣는다(배포 환경변수로 켠다).
