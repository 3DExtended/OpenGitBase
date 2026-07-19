<!-- forge: #222 -->

# [slice] sow-02 - Public windows history API

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #220

## What to build

Anonymous history endpoint for closed (and listable) outage windows.

1. **`GET /public/status/windows?days=`** - days 1..90 (default 7); return ordered windows: open group, then closed group, then partial; omit suppressed; include duration metadata; redacted instance labels.
2. **Projection** - headline subject rules: group labels for Website/API/Git/Storage/Message bus; Postgres/Redis for Data stores special-case; partial scope flagged for UI.
3. **Rate limit** - same anonymous posture as other public status reads.
4. **Tests** - days bounds, ordering, suppressed omitted, redaction, auth anonymous, rate-limit wiring.

Demo: after closed windows exist, curl windows API returns factual ranges with correct order and naming.

## Acceptance criteria

- [ ] Endpoint is anonymous and rate-limited
- [ ] `days` accepted 1..90 with sensible default (7)
- [ ] Response omits suppressed windows
- [ ] Order: open group -> closed group -> partial
- [ ] Data stores and partial naming/scope match PRD rules
- [ ] Instance labels use live public redaction path
- [ ] DTO includes id, scope, group, optional instance, start, optional end, open, duration, optional annotation
- [ ] API tests cover above behavior

## Blocked by

- #221


## User stories covered

- 4, 5, 8, 9, 10, 11, 17, 20, 23 (closed/archive), 24
