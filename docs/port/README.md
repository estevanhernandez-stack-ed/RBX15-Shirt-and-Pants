# docs/port

The v5 native port's working record. Design: [native v5 port design](../superpowers/specs/2026-10-11-native-v5-port-design.md).

- `parity-ledger.json` is the source of truth: every 4.1 behavior, its v5 owner, its check, its disposition and its status.
- `parity-ledger.md` and `divergences.md` are generated. Never edit them by hand. Change the JSON, then run `python tools/ledger/ledger.py render`.
- CI runs `python tools/ledger/ledger.py check`. It fails on unknown owners, checks, dispositions or statuses, duplicate IDs, fix or drop rows without a `4.1: ... v5: ...` sentence, and stale Markdown.
- A milestone closes a row by setting `"status": "done"` once that row's check passes. v5 ships only when no row is `open`.
- Frozen 4.1 render goldens live in `tests/goldens/`. See `tools/parity-ref/README.md` for when and how they may be regenerated.
