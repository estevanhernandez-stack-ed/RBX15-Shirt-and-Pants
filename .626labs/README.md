# 626 Labs integration

This folder holds metadata that the [626 Labs](https://626labs.dev) hub and 626 Labs Dashboard consume automatically.

## Files

| File | Consumed by | Purpose |
|---|---|---|
| `manifest.json` | [626labs.dev](https://626labs.dev) hub | Product card data — name, tagline, description, screenshots, distribution paths, feature list |

The hub fetches `.626labs/manifest.json` from every 626 Labs product repo on rebuild, generates a product card from the data, and links back to the repo's homepage + releases.

## Dashboard integration

The 626 Labs Dashboard (Agent OS) binds to this repo automatically by its git remote URL. No manifest file required on this side — the Dashboard queries its own project index.

When the MCP tool `mcp__626Labs__manage_projects` with `action: findByRepo` is called with this repo's URL, it returns the bound project's ID. Decisions logged from this repo via `mcp__626Labs__manage_decisions` are automatically tagged with that project ID.

## Updating the manifest

Bump `version` in `manifest.json` whenever `package.json` version bumps. Update `status` when distribution paths change (e.g., `distribution[].status: "pending-submission"` → `"live"` once the Microsoft Store listing is approved).

Keep the manifest in sync with reality — the hub surfaces whatever's here.
