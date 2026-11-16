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
