<!-- forge: #210 -->

# Redis cache, cache headers, anonymous rate limits

## Metadata

- ID: repo-browse-09
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Add **Redis** to the Docker Compose stack and wire the API to cache repository content responses.

**Defaults (no operator tuning required for v1):**

| Setting | Value |
|---------|--------|
| Redis image | `redis:7-alpine` |
| Service name | `redis` |
| API connection | `REDIS_URL=redis://redis:6379` |
| Content cache TTL | **60 seconds** |
| Cache key pattern | `repo-content:{repositoryId}:{endpoint}:{ref}:{pathHash}` |
| Public `Cache-Control` | `public, max-age=60` |
| Private `Cache-Control` | `no-store` (from issue 03) |
| Anonymous content rate limit | **120 requests / minute / IP** on content browse routes |
| Authenticated content rate limit | Use existing API rate limit policy (no extra cap) |
| Redis unavailable | Bypass cache, log warning, serve from storage (degraded but functional) |

Cache tree, blob metadata (not raw byte streams), readme, branches, and tags list responses. Do not cache raw download responses.

Apply `[EnableRateLimiting]` (or equivalent) to anonymous-accessible content endpoints with a dedicated `"content-browse-anonymous"` policy.

## Acceptance criteria

- [ ] Redis service added to docker-compose; API connects via `REDIS_URL`
- [ ] Second identical tree request within 60s does not call storage (verify via mock or metric/log assertion in test)
- [ ] Public tree response includes `Cache-Control: public, max-age=60`
- [ ] Private tree response includes `Cache-Control: no-store`
- [ ] Anonymous client exceeding 120 req/min on content routes receives 429
- [ ] Authenticated user not subject to the stricter anonymous content limit
- [ ] API continues to serve content when Redis is down (cache bypass)
- [ ] Unit tests for cache get/set key formation and TTL behavior
- [ ] API integration test: rate limit returns 429 after threshold
- [ ] API integration test: cache hit skips storage HTTP client (mocked)

## Blocked by

- [03-private-repository-content-authorization.md](./03-private-repository-content-authorization.md)

## User stories covered

- 38, 39, 40

## Notes

- Use `StackExchange.Redis` or existing project cache abstraction if present.
- Raw blob downloads are uncached streams to avoid memory pressure.
- Document Redis in README local development section (one line: started by default compose).
