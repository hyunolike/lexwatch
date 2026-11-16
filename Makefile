# 기존 DOCKER_API_VERSION 환경변수는 그대로 통과한다 (구형 Docker 로컬 실행용).
.PHONY: test-backend
test-backend:
	cd backend && dotnet test LexWatch.sln
