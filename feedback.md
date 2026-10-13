# feedback.md — bug reports & user feedback

> Seeded 2026-07-21 from the full-code audit (The Architect, RBX15 revival session).
> These four are confirmed-in-code findings cited to file:line — not end-user reports.
> Add real user reports below as they arrive (one per line, optional [severity] tag).

[fixed in PR #27] bug: [critical] Layer name is interpolated unescaped into innerHTML (editor.js:674, `value="${sel.name}"`). With nodeIntegration:true + contextIsolation:false (main.js:10-11), a crafted asset filename executes code with Node access the moment the layer is selected.
[fixed in PR #25] bug: [major] Asset browser click-to-add is broken — `clickName` is undefined at editor.js:2232 (variable in scope is `displayName`). Every thumbnail click throws a ReferenceError inside img.onload and no layer is added. Happens every time; advertised README feature.
[fixed in PR #26] bug: [major] Eraser tool is a no-op — destination-out strokes land on an empty offscreen canvas (editor.js:1027-1035), so the mouseup content check finds nothing and discards the stroke. E key and button both dead. Happens every time; advertised README feature.
[fixed in PR #28] bug: [minor] No redo, and undo doesn't cover region fills, background changes, or mode switches — toggling Shirt/Pants wipes all region colors irrecoverably (editor.js:1457).
