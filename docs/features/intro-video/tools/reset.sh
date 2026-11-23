#!/bin/zsh
T=$(docker exec lexwatch-work-api-1 printenv JOB_TOKEN)
curl -s -o /dev/null -w "%{http_code}\n" -X POST -H "X-Job-Token: $T" http://127.0.0.1:5080/jobs/reset-demo
