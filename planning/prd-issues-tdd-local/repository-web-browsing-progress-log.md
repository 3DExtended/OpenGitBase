# Repository web browsing — progress log

| When | Issue | Outcome |
|------|-------|---------|
| 2026-06-21 | repo-browse-01 | Storage content API + Python tests |
| 2026-06-21 | repo-browse-02–11 | API, web UI, Redis, e2e script, 20 API unit tests |

Commits on `main`:
- `d8460aa` feat(storage): add git content read HTTP API
- `92b7a05` feat(api): repository web content browsing endpoints
- `cb5b734` feat(web): repository file tree, blob view, and README

Local verification:
- `dotnet test` — 334 API tests pass; 2 pre-existing Common.Tests DI failures
- `python3 -m unittest test_storage_content.py` — 7 pass
- Docker stack rebuilt; browser smoke test on empty public repo OK
- `scripts/test-repo-browse-e2e.sh` — pass (empty repo fixture)
