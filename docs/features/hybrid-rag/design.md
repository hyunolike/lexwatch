# 설계: 조문 하이브리드 검색

- 명세: [spec.md](spec.md)
- 상태: 2차 EGM 검토 반영, 구현 중(2026-11-20, [1차](reviews/2026-11-20-round1.md), [2차](reviews/2026-11-20-round2.md))

## 1. 선택한 구조

```
fetch_articles → classify ─(진행할 그룹이 있을 때만)→ index_articles ─(그룹마다)→ analyze → critic_citation → critic_source → critic_judge → notify
                                                       │                              │
                                                       ├ 청킹, 토큰화(C#)             ├ 질의 토큰 + 질의 임베딩(/embed)
                                                       ├ /embed(passage, bge-m3)      ├ HybridSearch SQL(BM25 + 코사인, RRF)
                                                       └ ArticleChunks 저장           └ 변경 조문 ≤12 + 검색 ≤8 → /analyze
```

- **검색은 PostgreSQL 한 쿼리에서 한다.** BM25를 SQL로 계산하고(토큰 배열 `text[]`을 펼쳐 df, tf를 집계), pgvector 코사인과 RRF로 합친다. 근거와 같은 DB, 같은 실행 범위다.
- **BM25를 SQL로 직접 계산하는 이유.** PostgreSQL 전문 검색의 `ts_rank_cd`에는 IDF가 없어 "하여", "법률" 같은 흔한 바이그램이 순위를 끌고 간다. 지금 AI 서비스의 BM25(`ai/app/rag/lexical.py`)와 같은 공식을 SQL로 옮기면 기준선이 약하지 않고, 두 구현의 순위가 같은지 테스트로 확인할 수 있다. 실행당 청크가 수백 개라 집계 비용은 작다.
- **토큰화는 C#에 둔다.** 색인과 질의가 같은 코드로 토큰을 만든다. `lexical.py`의 `tokenize`를 그대로 옮긴다: 소문자로 바꾸고 `[0-9a-z가-힣]+`로 어절을 뽑은 뒤, 한글이 섞인 어절은 숫자를 포함한 2글자 바이그램을 더한다("제56조" → 제56조, 제5, 56, 6조).
- **AI 서비스는 임베딩만 한다.** 지금의 `/embed`에 Cloudflare `@cf/baai/bge-m3` 공급자를 더한다.

## 2. 대안과 고르지 않은 이유

| 대안 | 고르지 않은 이유 |
|---|---|
| PostgreSQL 전문 검색(`tsvector`, `ts_rank_cd`) | IDF가 없어 기준선이 약하다. `to_tsvector('simple')`이 C# 토큰을 다시 쪼갤 수 있어 색인과 질의 토큰이 어긋날 위험이 있다 |
| AI 서비스 안의 메모리 검색 | Vercel 함수는 상태가 없어 매 분석마다 조문 전체를 다시 임베딩한다 |
| 별도 벡터 DB | 실행당 수백 청크 규모에 운영 부품만 는다. 근거와 같은 트랜잭션에 묶을 수 없다 |
| 근사 색인(HNSW) | 검색은 항상 한 실행으로 좁혀 정확 탐색이 빠르고 결정적이다 |
| `fetch_articles` 바로 뒤에서 색인 | 관련 없음으로 닫힐 실행(첫 공개 수집 17건 중 15건)까지 색인해 무료 DB(500MB)와 Neurons를 낭비한다 |

## 3. 데이터

### 3.1 엔터티와 마이그레이션 `ArticleChunks`

```csharp
public class ArticleChunk
{
    public Guid RunId { get; set; }
    public string ChunkId { get; set; } = "";     // "law:001872:제56조#2"
    public string EvidenceId { get; set; } = "";  // "law:001872:제56조"
    public int Ord { get; set; }                  // 실행 안 전체 순번(근거 자연 정렬 → n)
    public string Text { get; set; } = "";        // 근거 제목 + 줄바꿈 + 청크 본문
    public string[] Tokens { get; set; } = [];    // C# 토크나이저 결과(중복 포함, BM25 tf용)
    public Vector? Embedding { get; set; }        // vector(1024), 실패 시 null
    public string? EmbedModel { get; set; }
}
```

- 키 `(RunId, ChunkId)`. `(RunId, EvidenceId)` → `Evidence(RunId, EvidenceId)` 외래 키, `OnDelete(Cascade)`.
- 열 타입: `Tokens` `text[]`, `Embedding` `vector(1024)`. 상수 `LexWatchDb.ChunkEmbedDim = 1024`(실제 차원은 작업 1에서 확인).
- `PracticeGroups.Embedding`(vector(2048))은 이번에 바꾸지 않는다(7절).
- 크기 추정: 청크 1개 ≈ 벡터 4KB + 본문 1~2KB + 토큰 배열 2~4KB. 관련 있는 실행(첫 수집 기준 17건 중 2건)만 색인하므로 실행당 1~2MB, 하루 수 MB다. 종결된 실행의 청크 정리는 미정 사항 3.
- 테스트 리셋(`PostgresFixture.ResetAsync`)에 `"ArticleChunks"`를 명시한다.

### 3.2 청킹 규칙 (RAG-01)

- 대상: 실행의 `kind = "law"` 근거(`fetch_articles`가 저장한 것). 에이전트가 나중에 더한 `law_article`, `old_new`와 신구조문 `oldnew`는 색인하지 않는다.
- `Evidence.Body`는 `LawApiClient.FlattenArticle`이 조문내용, 항, 호, 목을 줄바꿈으로 이어 붙인 텍스트다. 첫 줄은 대개 "제N조(제목)"이라 제목이 본문에 이미 있다.
- 청크 텍스트 = `Evidence.Title` + "\n" + 청크 본문.
- 본문 앞의 "제N조(제목)"(정규식 `^제\d+조(?:의\d+)?(?:\([^)]*\))?`)을 떼어 낸다. 실제 본문 첫 줄은 "제29조(안전조치의무) 개인정보처리자는…"처럼 제목과 내용이 한 줄에 있다.
- 본문 ≤ 1,200자: 청크 하나(`#1`).
- 초과: 원문자(①~⑳)로 시작하는 줄을 항 경계로 본다. 항이 1,200자를 넘으면 줄(호·목) 단위로, 줄이 넘으면 "다." 뒤 공백을 문장 경계로 나눈다(녹화본의 긴 조문은 대부분 호 목록이다). 조각을 순서대로 1,200자까지 묶는다. 1,200자는 제목 접두를 뺀 본문 길이다.
- 본문이 비었거나 "삭제"로만 된 조문: 청크를 만들지 않는다.
- 자연 정렬: `제2조 < 제10조 < 제10조의2`. 조 번호와 가지 번호를 숫자로 비교하는 `ArticleOrder.Compare`를 만들어 청크 `Ord`와 변경 조문 정렬에 함께 쓴다.

## 4. 검색 (RAG-03)

`HybridSearch.SearchAsync(LexWatchDb db, Guid runId, string[] queryTokens, float[]? queryVector, string? model, SearchMode mode, int topEvidence)` → `IReadOnlyList<RetrievalHit>(EvidenceId, ChunkId, int? BmRank, int? DenseRank, double Rrf)`.

- `mode`: `Bm25`, `Dense`, `Hybrid`. 평가와 운영이 같은 함수를 쓴다.
- `queryVector`가 null이거나 길이가 `ChunkEmbedDim`이 아니거나 `model`이 null이면 dense CTE가 없는 쿼리를 쓴다(타입 없는 null 매개변수와 차원 오류를 피한다).
- 질의 토큰은 5.3의 출처별 할당으로 만들어 넘긴다(최대 96개). 토큰이 0개면 BM25 CTE를 뺀다.
- SQL은 청크 단위로 최대 100행을 받고, C#에서 근거별 최고 순위만 남겨 `topEvidence`개를 고른다.

```sql
WITH docs AS (
  SELECT "ChunkId", "Ord", "Tokens", cardinality("Tokens") AS len
  FROM "ArticleChunks" WHERE "RunId" = @run
), stats AS (
  SELECT count(*)::float8 AS n, avg(len)::float8 AS avgdl FROM docs
), hits AS (                                  -- 질의 토큰과 겹치는 (청크, 토큰, tf)
  SELECT d."ChunkId", t.tok, count(*)::float8 AS tf, d.len
  FROM docs d CROSS JOIN LATERAL unnest(d."Tokens") AS t(tok)
  WHERE t.tok = ANY(@qtoks)
  GROUP BY d."ChunkId", t.tok, d.len
), df AS (
  SELECT tok, count(*)::float8 AS df FROM hits GROUP BY tok
), bm25 AS (
  SELECT h."ChunkId",
         sum(ln(1 + (s.n - df.df + 0.5) / (df.df + 0.5))
             * h.tf * (1.4 + 1) / (h.tf + 1.4 * (1 - 0.75 + 0.75 * h.len / s.avgdl))) AS score
  FROM hits h JOIN df USING (tok) CROSS JOIN stats s
  GROUP BY h."ChunkId"
), bm AS (
  SELECT b."ChunkId", row_number() OVER (ORDER BY b.score DESC, d."Ord", b."ChunkId") AS r
  FROM bm25 b JOIN docs d USING ("ChunkId") ORDER BY r LIMIT 50
), dense AS (                                 -- 벡터가 있을 때만 포함
  SELECT "ChunkId", row_number() OVER (ORDER BY "Embedding" <=> @qvec, "Ord", "ChunkId") AS r
  FROM "ArticleChunks"
  WHERE "RunId" = @run AND "EmbedModel" = @model AND "Embedding" IS NOT NULL
  ORDER BY r LIMIT 50
)
SELECT c."EvidenceId", c."ChunkId", bm.r::int AS "BmRank", dense.r::int AS "DenseRank",
       (coalesce(1.0 / (60 + bm.r), 0) + coalesce(1.0 / (60 + dense.r), 0))::float8 AS "Rrf"
FROM bm FULL JOIN dense ON bm."ChunkId" = dense."ChunkId"
JOIN "ArticleChunks" c ON c."RunId" = @run AND c."ChunkId" = coalesce(bm."ChunkId", dense."ChunkId")
ORDER BY rrf DESC, c."Ord", c."ChunkId"
LIMIT 100;
```

- `@qvec`는 `vector` 타입으로 명시한 매개변수다. 모드에 따라 `bm`이나 `dense` CTE와 해당 결합을 뺀 쿼리를 문자열로 조합한다(3가지).
- 결과 매핑은 `db.Database.SqlQueryRaw<RetrievalRow>`. 열 별칭을 속성 이름(`BmRank`, `DenseRank`, `Rrf`)에 맞추고 `row_number()`(bigint)와 RRF(numeric)를 `int`, `float8`로 바꾼다.
- `topEvidence` = 8 + 이번에 보낼 변경 조문 수(변경 조문과 같은 조문을 빼고도 8건을 채우기 위해). `Dense` 모드인데 벡터가 없거나, 토큰과 벡터가 모두 없으면 쿼리를 보내지 않고 0건을 낸다.
- BM25 공식은 `lexical.py`의 `rerank`와 같다(idf = ln(1 + (N − df + 0.5)/(df + 0.5))). 다만 `lexical.py`는 질의 토큰 중복을 그대로 더하고 이 SQL은 중복을 지운다. 차이를 테스트에 적는다.

## 5. 단계

### 5.1 `classify` (변경 최소)

진행할 그룹(`ClosedIrrelevant`가 아닌 그룹)이 하나라도 있으면 그룹별 `analyze` 대신 `NextStep("index_articles", null, null)` 하나를 낸다. 그 밖의 계산(그룹 유사도, 판정, 상태)은 그대로다.

### 5.2 `index_articles` (새 단계, RAG-02, RAG-05)

1. 실행의 `law` 근거를 자연 정렬로 읽어 청킹하고 토큰화한다(`ArticleIndexer.Build`, 순수 함수).
2. 임베딩(`IChunkEmbedder`, 운영 구현은 `AiGateway.EmbedAsync(passage)`): 50개씩 보낸다. **모두 성공해야 쓴다.** 한 배치라도 실패하거나, 응답 차원이 `ChunkEmbedDim`이 아니거나, 공급자 kind가 `fallback`이면 즉시 멈추고 모든 청크의 임베딩을 null로 둔다. 단계 전체 임베딩 예산 90초(배치마다 남은 시간으로 제한).
3. 저장: 실행의 기존 청크를 EF로 읽어 키가 같으면 값을 고치고, 새 키는 추가하고, 남은 키는 `RemoveRange`한다. 원시 SQL을 쓰지 않는다. 워커가 소유권 확인 뒤 같은 트랜잭션에서 저장한다(`WorkflowWorker` 86~106행).
4. 다음 단계: 실행의 그룹 가운데 `ClosedIrrelevant`가 아닌 그룹마다 `analyze`.
5. 출력: `{ chunks, embedded, dense: "ok" | "unavailable", reason?: "embed_failed" | "fallback_provider" | "dim_mismatch" | "budget", model? }`. ProviderKind는 임베딩 성공 시 `AiMapping.ProviderKind(provider)`, 실패 시 `fallback`.

변경 지점: `Program.cs` 핸들러 등록, `RetryPolicy`(`index_articles` = 3), `web/src/lib/labels.ts` 단계 라벨, 재현 기록(`fixtures/demo/replays/*.json`)의 단계 목록 확인.

임베딩 실패를 `Retry`로 돌리지 않는 이유: 무료 한도 소진은 몇 시간 풀리지 않는다. 파이프라인을 묶지 않고 저하를 출력에 남긴다.

### 5.3 `analyze` (RAG-04, RAG-07)

1. 실행에 청크가 없으면 지금처럼 근거 전체를 보내고 `retrieval.status = "no_index"`를 남긴다.
2. 변경 조문: 실행의 `oldnew` 근거를 자연 정렬로 최대 12건. 넘으면 `oldnewTruncated: true`.
3. 질의는 세 출처에서 토큰을 할당량대로 모은다(`RetrievalQuery.Build`, 중복은 먼저 들어온 쪽만 남긴다).
   - 새 구간 48개: 변경 조문 본문에서 `[신조문]\n` 뒤 부분 문자열을 잘라 `ChangeMarksJson`의 `new` 구간을 적용한 텍스트. 구간이 없으면 신조문 앞 200자.
   - 조문 제목 16개: 변경 조문의 괄호 안 제목만(예: "연장·야간 및 휴일 근로"). 법령명과 "신구조문 대비"는 잡음이라 넣지 않는다.
   - 관심 분야 32개: 실무그룹 `InterestText`.
   - 변경 조문이 없으면 관심 분야 + 법령명.
   - 벡터 질의 텍스트는 같은 세 출처를 이어 붙인 문장이다(토큰 할당과 같은 순서).
4. 질의 벡터 = `/embed`(query). 이 호출은 10초 제한이고 실패하면 null(단계 실패로 돌리지 않는다). 청크의 `EmbedModel`과 응답 공급자 이름이 다르면 null.
5. `HybridSearch`(설정 `Retrieval:Mode`, 기본 `Hybrid`)로 근거 상위를 받고, 변경 조문과 조문 번호가 같은 `law` 근거를 빼고 8건을 채운다.
6. 보낼 근거 = 변경 조문 + 검색 8건. 순서를 유지하는 매핑을 쓴다(`AiMapping.Evidence`는 ID 문자열순으로 다시 정렬하므로 `AiMapping.EvidenceInOrder`를 더한다).
7. 둘 다 0건이면 근거 전체를 보내고 `retrieval.status = "fallback_all"`.
8. 출력(300자 안): `retrieval: { status: "ok" | "no_index" | "fallback_all", mode, sent, oldnew, searched, denseUsed: bool, top: ["제56조", ...최대 5] }`. 출력 JSON은 한글을 `\uXXXX`로 바꾸므로(약 3배) 조문 번호만 넣고 이유 값은 영문 코드로 쓴다.

크리틱은 이미 인용한 근거만 받는다(CF-07). 크리틱 1·2단은 실행의 근거 전체를 DB에서 읽으므로 인용 검증은 바뀌지 않는다.

## 6. AI 서비스

- `CloudflareRetrieverProvider(NvidiaRetrieverProvider)`: base URL `https://api.cloudflare.com/client/v4/accounts/{id}/ai/v1`, 모델 `@cf/baai/bge-m3`, `info = ProviderInfo(name="@cf/baai/bge-m3", kind="cloudflare")`. 요청 본문에서 NVIDIA 전용 필드(`input_type`, `truncate`)를 받는지 작업 1에서 확인하고, 받지 않으면 뺀다. bge-m3는 질의와 문서에 별도 접두가 필요 없다.
- `rerank`는 `LexicalRetrieverProvider.rerank`(BM25)에 위임한다. 판례와 행정규칙 도구의 재순위는 지금과 같다.
- **실패 시 대체**: Cloudflare 임베딩이 실패하면(429, 5xx, 연결) AI 서비스가 lexical 임베딩(256차원)으로 바꿔 응답하고 `provider.kind = "fallback"`을 단다. 그래서 `/embed`는 지금처럼 실패하지 않고, `classify`는 지금 흐름 그대로 동작한다. 백엔드는 kind와 차원으로 저하를 알아챈다.
- `get_retriever()` auto 순서: Cloudflare 값이 있으면 Cloudflare, 없고 NVIDIA 키가 있으면 NVIDIA, 둘 다 없으면 lexical. LLM 선택 순서(NVIDIA → Cloudflare)와 반대인 것은 의도다: NVIDIA 임베딩(2,048차원)은 청크 열과 맞지 않는다.

## 7. 분류의 그룹 유사도

`classify`는 지금처럼 `/embed`로 개정 요약과 그룹 관심 분야를 임베딩해 코사인을 계산한다. 공급자가 Cloudflare로 바뀌면 bge-m3(1,024차원) 벡터가 오지만 `PracticeGroups.Embedding`(2048)에 맞지 않아 저장되지 않고 매번 계산한다(지금 대체 구현과 같은 경로, `ClassifyStep` 54행). 유사도는 LLM 분류 프롬프트에 힌트로만 들어간다. 대체 분류(LLM 없음)의 문턱 0.5는 bge-m3 분포에서 다시 정해야 하므로, 평가 때 녹화 법령 × 3그룹 유사도 분포를 함께 기록한다. 열 차원 변경은 다음 작업이다.

## 8. 평가 (RAG-06)

- **질의셋** `fixtures/retrieval/queries.jsonl`: `{ "id", "law_id": "001872", "query", "answers": ["제56조"], "type": "keyword" | "paraphrase" | "operational" }`. 녹화본 `fixtures/lawapi/recorded/current/law_001872.json`(133조), `law_011357.json`(127조), `law_010199.json`(71조). 정답은 근거 ID와 같은 "제N조" 표기. 운영형은 운영 질의 만들기(5.3의 3)와 같은 함수로 관심 분야 + 가상 변경 조문에서 만든다. 작성자가 썼다는 한계를 결과에 적는다.
- **임베딩 캐시** `fixtures/retrieval/embeddings.json`: `{ "model", "dim", "vectors": { sha256(model + "\0" + input_type + "\0" + text): [소수 5자리...] } }`.
  - `make eval-retrieval-texts`: .NET 평가가 필요한 (input_type, text) 목록을 `fixtures/retrieval/needed.jsonl`로 쓴다.
  - `make eval-retrieval-embed`: `scripts/embed_cache.py`가 캐시에 없는 것만 Cloudflare로 계산한다(`CLOUDFLARE_*` 환경변수, 전체 약 100 Neurons).
  - 평가 실행 중 캐시에 없는 텍스트가 있으면 네트워크로 가지 않고 누락 목록과 함께 실패한다.
- **실행** `make eval-retrieval` = `dotnet test backend/LexWatch.sln --filter Category=eval`. 평가 테스트(`[Trait("Category","eval")]`)가 Testcontainers PostgreSQL에 법령마다 실행 하나를 만들어 `ArticleIndexer`와 캐시 임베더로 색인하고, `HybridSearch`를 세 모드로 돌린다.
- **CI 제외**: `.github/workflows/ci.yml`과 `Makefile`의 `dotnet test`에 `--filter "Category!=eval"`을 더한다.
- **결과** `eval/results/retrieval_<model>_<날짜>.json`: 모드별, 유형별 recall@5, recall@10, MRR, 질의별 순위와 승패, 분류 유사도 분포.
- **BM25 동등성**: 같은 청크와 질의에서 SQL BM25 순위가 `lexical.py`와 같은지 확인하는 테스트(토큰 중복 처리 차이는 질의 토큰을 중복 제거해 맞춘다). 토크나이저는 공용 골든 파일 `fixtures/retrieval/tokenizer_golden.json`을 C# 테스트와 pytest가 함께 통과한다.

## 9. 실패 처리 요약

| 상황 | 동작 | 보이는 곳 |
|---|---|---|
| 색인 임베딩 실패, 대체, 차원 불일치 | 키워드 색인만 | `index_articles` 출력 `dense: "unavailable"`, 출처 라벨 대체 구현 |
| 질의 임베딩 실패, 모델 불일치 | BM25 경로만 | `analyze` 출력 `dense: false` |
| 색인 없음 | 근거 전체 | `retrieval: "no_index"` |
| 검색과 변경 조문 모두 0건 | 근거 전체 | `retrieval: "fallback_all"` |
| `index_articles` 처리 중 예외 | 워커 재시도(최대 3), 그 뒤 실행 실패 | 타임라인. 임베딩 실패는 예외가 아니라 저하로 처리하므로 여기 오는 것은 DB 오류 같은 진짜 장애뿐이다. 분석이 `no_index`로 진행하게 바꾸면 장애를 숨기므로 실패로 둔다 |

## 10. 일정과 자를 순서

한도가 풀리기 전(1단계)에 한도 없이 되는 것을 끝낸다.

1. 1단계(한도 불필요): 토크나이저, 청킹, 마이그레이션, BM25 SQL, `index_articles`(임베딩 없으면 키워드만), `analyze` 입력 축소, 평가 틀(BM25 모드). 이것만으로 명세의 문제 1, 2는 해결된다.
2. 2단계(한도 필요): 차원 확인, Cloudflare 임베딩 공급자, 캐시 생성, 벡터와 하이브리드 평가, 기본 모드 결정.
3. 시간이 모자라면 자르는 순서: 운영형 질의 유형 → 인용 재현율 → 분류 유사도 분포.

## 11. 미정 사항

1. bge-m3 실제 출력 차원과 요청 필드 허용 여부. 작업 1에서 한 번 호출해 녹화한다.
2. 하이브리드가 BM25보다 낫지 않으면 운영 기본값을 BM25로 둔다(명세 성공 판단).
3. 종결된 실행의 청크 정리(용량). 지금 규모에서는 급하지 않아 다음 작업으로 둔다.
4. 에이전트 도구 `get_article`은 `law:001872:56` 형식으로 근거를 더해 `fetch_articles`의 `law:001872:제56조`와 같은 조문이 두 ID로 남는다(기존 문제). 이번 범위에서 고치지 않는다.
