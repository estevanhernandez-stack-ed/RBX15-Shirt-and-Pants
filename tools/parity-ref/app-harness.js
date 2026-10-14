// Injected by app-render.js into the REAL editor.html after it loads. It drives 4.1 the way a
// person does: the page's own globals (switchMode, makeLayer, setTool, selectLayer, exportPNG,
// saveProject), its real inputs and buttons, and synthetic mouse events on #mainCanvas. Nothing in
// editor.js is replaced. window.alert and window.confirm are stubbed and anchor clicks intercepted,
// so a hidden window never blocks and downloads are captured instead of written.
(function () {
  'use strict';
  if (window.__pref) return;

  const SAVED_AT_PINNED = '2026-10-11T00:00:00.000Z';
  const alerts = [];
  window.alert = (message) => { alerts.push(String(message)); };
  window.confirm = () => true; // exportPNG's empty-template guard, editor.js:1504-1506

  // Pin the canvas at the viewport origin so synthesized client coordinates are integers. The
  // handlers map them back with (clientX - rect.left) / zoom (editor.js:1059-1061, 1150-1152).
  const pin = document.createElement('style');
  pin.textContent = '#mainCanvas { position: fixed !important; left: 0 !important; top: 0 !important; margin: 0 !important; }';
  document.head.appendChild(pin);

  const byId = (id) => {
    const el = document.getElementById(id);
    if (!el) throw new Error(`#${id} not found in editor.html`);
    return el;
  };
  const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  async function waitFor(done, what, ms = 15000) {
    const start = performance.now();
    while (!done()) {
      if (alerts.length) throw new Error(`4.1 raised an alert while waiting for ${what}: ${alerts.join(' | ')}`);
      if (performance.now() - start > ms) throw new Error(`timed out waiting for ${what}`);
      await sleep(5);
    }
  }

  function loadImg(src) {
    return new Promise((resolve, reject) => {
      if (!src) { reject(new Error('missing image data')); return; }
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error('image decode failed'));
      img.src = src;
    });
  }

  // Set a real control and fire the event its 4.1 listener uses. The value must stick: range and
  // color inputs sanitize, and a scene that silently clamps would freeze the wrong golden.
  function setInput(id, value, eventName = 'input') {
    const el = byId(id);
    el.value = String(value);
    if (el.value !== String(value)) throw new Error(`#${id} refused ${value} (holds ${JSON.stringify(el.value)})`);
    el.dispatchEvent(new Event(eventName, { bubbles: true }));
  }

  function setCheck(id, on) {
    const el = byId(id);
    el.checked = on;
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function mouse(type, point) {
    const [x, y] = point;
    if (!Number.isInteger(x) || !Number.isInteger(y)) throw new Error(`template point ${JSON.stringify(point)} must be integers`);
    const c = byId('mainCanvas');
    const rect = c.getBoundingClientRect();
    if (!Number.isInteger(rect.left) || !Number.isInteger(rect.top)) throw new Error(`canvas not at integer client coordinates (${rect.left}, ${rect.top})`);
    const clientX = rect.left + x * zoom; // zoom: editor.js:81, 1 unless changed
    const clientY = rect.top + y * zoom;
    const ev = new MouseEvent(type, { clientX, clientY, button: 0, buttons: type === 'mouseup' ? 0 : 1, bubbles: true, cancelable: true, view: window });
    if (ev.clientX !== clientX || ev.clientY !== clientY) throw new Error(`MouseEvent rounded (${clientX}, ${clientY})`);
    c.dispatchEvent(ev);
  }

  // Base scene fields, applied as the app's own state. Defaults: editor.js:76-107 and makeLayer
  // (editor.js:134-142). A fresh page has nextId = 1 (editor.js:499), so layer ids are 1..n.
  async function applyBase(scene, images) {
    const mode = scene.mode || 'Shirt';
    if (mode !== currentMode) switchMode(mode); // editor.js:1695-1717
    bgColor = scene.bgColor ?? '#192e44';
    bgOpacity = scene.bgOpacity ?? 1;
    bgImage = scene.bgImage ? await loadImg(images[scene.bgImage]) : null;
    regionColors = JSON.parse(JSON.stringify(scene.regionColors || {}));
    document.querySelectorAll('.region-btn').forEach((b) => paintRegionButton(b, regionColors[b.dataset.r] || null)); // as editor.js:2615-2618
    layers = [];
    const ids = [];
    for (const [i, d] of (scene.layers || []).entries()) {
      const layer = makeLayer(await loadImg(images[d.image]), 'L' + (i + 1), d.x ?? 0, d.y ?? 0, d.scale ?? 1, {
        visible: d.visible ?? true, opacity: d.opacity ?? 1, rotation: d.rotation ?? 0,
        flipH: !!d.flipH, flipV: !!d.flipV,
        brightness: d.brightness ?? 100, contrast: d.contrast ?? 100, saturate: d.saturate ?? 100,
        hueRotate: d.hueRotate ?? 0, rShift: d.rShift ?? 0, gShift: d.gShift ?? 0, bShift: d.bShift ?? 0,
      });
      layers.push(layer);
      ids.push(layer.id);
    }
    selectedLayerId = null;
    updateUI();
    render();
    return ids;
  }

  const SHAPE_TOOLS = { line: 'line', rect: 'rect', ellipse: 'circle' }; // tool ids, editor.js:464-471

  async function shape(op) {
    const tool = SHAPE_TOOLS[op.tool];
    if (!tool) throw new Error(`unknown shape tool "${op.tool}"`);
    setTool(tool); // editor.js:473-478
    setInput('drawColor', op.color); // editor.js:492
    setInput('drawSize', op.width); // editor.js:493; range 1..50 (editor.html:57)
    setCheck('drawFill', !!op.fill); // editor.js:494
    const before = layers.length;
    mouse('mousedown', op.from); // editor.js:1121-1146
    mouse('mousemove', op.to); // clear and redraw the shape, editor.js:1204-1233
    mouse('mouseup', op.to); // commit via addDrawingLayer, editor.js:1249-1293
    await waitFor(() => layers.length === before + 1, `the ${op.tool} drawing layer (editor.js:528-540)`);
  }

  async function erase(op, baseIds) {
    const id = baseIds[op.layer];
    if (id === undefined) throw new Error(`scene has no layer ${op.layer}`);
    const layer = layers.find((l) => l.id === id);
    if (!layer) throw new Error(`layer ${op.layer} is no longer in the document`);
    if (!Array.isArray(op.points) || op.points.length < 2) {
      throw new Error('erase needs at least 2 points: a lone click strokes nothing (editor.js:1253-1255 strokes at mouseup only for the pen)');
    }
    selectLayer(id); // editor.js:575-580
    setTool('eraser');
    setInput('drawSize', op.size);
    const img = layer.img;
    mouse('mousedown', op.points[0]);
    for (const p of op.points.slice(1)) mouse('mousemove', p); // lineTo + stroke per move, editor.js:1200-1203
    mouse('mouseup', op.points[op.points.length - 1]);
    await waitFor(() => layer.img !== img, `the erased bitmap of layer ${op.layer} (editor.js:1265-1283)`);
  }

  async function detail(op) {
    if (currentMode !== 'Shirt') throw new Error('detail ops run in Shirt mode only: every 4.1 generator throws in Pants mode');
    if (!/^[a-z-]+$/.test(op.type)) throw new Error(`bad detail type "${op.type}"`);
    const btn = document.querySelector(`.detail-btn[data-detail="${op.type}"]`);
    if (!btn) throw new Error(`no detail button for "${op.type}" (editor.html:187-222)`);
    setInput('detailColor', op.color); // read by createDetailLayer, editor.js:1840
    setInput('detailWidth', op.width); // range 1..8, editor.html:181
    setCheck('detailDash', !!op.stitch); // editor.js:1842, 1849
    const before = layers.length;
    btn.click(); // editor.js:2077-2103 -> createDetailLayer, editor.js:1834-1865
    await waitFor(() => layers.length === before + 1, `the ${op.type} detail layer`);
  }

  const WORDART_INPUTS = [['text', 'waText'], ['fill', 'waColor'], ['stroke', 'waStroke'], ['grad', 'waColor2'],
    ['size', 'waSize'], ['strokeWidth', 'waStrokeW'], ['angle', 'waAngle']]; // editor.html:229-262

  async function wordart(op) {
    for (const [key, id] of WORDART_INPUTS) if (op[key] !== undefined) setInput(id, op[key]);
    if (op.font !== undefined) setInput('waFont', op.font, 'change');
    if (op.style !== undefined) {
      const btn = document.querySelector(`.wordart-style-btn[data-style="${op.style}"]`);
      if (!/^[a-z]+$/.test(op.style) || !btn) throw new Error(`unknown word art style "${op.style}"`);
      btn.click(); // sets waStyle, editor.js:2111-2118
    }
    // The canvas fetches a web font lazily, on first use, so 4.1 can draw the fallback font
    // (ledger wordart-font-loading-race). Load the exact face renderWordArt asks for first
    // (editor.js:2125, 2131).
    const family = byId('waFont').value;
    const spec = `900 ${+byId('waSize').value || 64}px "${family}"`;
    const faces = await document.fonts.load(spec);
    await document.fonts.ready;
    if (!faces.length || faces.some((f) => f.status !== 'loaded') || !document.fonts.check(spec)) {
      throw new Error(`vendored font did not load for ${spec}`);
    }
    const before = layers.length;
    byId('btnAddWordArt').click(); // editor.js:2257-2291
    await waitFor(() => layers.length === before + 1, 'the word art layer');
  }

  async function project(op, projects) {
    const p = projects[op.file];
    if (!p) throw new Error(`project file ${op.file} was not provided`);
    const data = JSON.parse(p.text);
    const want = (data.layers || []).filter((d) => d.imgSrc).length;
    if (!want) throw new Error('a project fixture needs at least one image layer, or its load cannot be observed');
    const before = layers;
    const input = byId('projectFileInput');
    const dt = new DataTransfer();
    dt.items.add(new File([p.text], p.name, { type: 'application/json' }));
    input.files = dt.files;
    input.dispatchEvent(new Event('change', { bubbles: true })); // editor.js:2684-2687 -> loadProject, 2593-2680
    await waitFor(() => layers !== before && layers.length === want &&
      (!data.bgImageSrc || (bgImage !== null && bgImage.src === data.bgImageSrc)), `project ${op.file} to finish loading`);
  }

  async function run(scene, assets) {
    alerts.length = 0;
    await document.fonts.ready;
    const ids = await applyBase(scene, assets.images);
    for (const [i, op] of (scene.actions || []).entries()) {
      try {
        if (op.op === 'shape') await shape(op);
        else if (op.op === 'erase') await erase(op, ids);
        else if (op.op === 'detail') await detail(op);
        else if (op.op === 'wordart') await wordart(op);
        else if (op.op === 'project') await project(op, assets.projects);
        else throw new Error(`unknown op "${op.op}"`);
        if (alerts.length) throw new Error(`4.1 raised an alert: ${alerts.join(' | ')}`);
      } catch (e) {
        throw new Error(`${scene.name} action ${i} (${op.op}): ${e.message}`);
      }
    }
  }

  // Run fn and return what the anchor it clicks would have downloaded.
  function captureDownload(fn) {
    const realClick = HTMLAnchorElement.prototype.click;
    let got = null;
    HTMLAnchorElement.prototype.click = function () { got = { href: this.href, download: this.download }; };
    try { fn(); } finally { HTMLAnchorElement.prototype.click = realClick; }
    if (!got) throw new Error('no download link was clicked');
    return got;
  }

  window.__pref = {
    async render(scene, assets) {
      await run(scene, assets);
      const { href } = captureDownload(() => exportPNG()); // editor.js:1500-1540
      if (!href.startsWith('data:image/png;base64,')) throw new Error('exportPNG did not produce a PNG data url');
      return href;
    },

    async save(scene, assets) {
      await run(scene, assets);
      const realCreate = URL.createObjectURL;
      let blob = null;
      URL.createObjectURL = (b) => { blob = b; return realCreate.call(URL, b); };
      // saveProject stamps savedAt: new Date().toISOString() (editor.js:2554). Pin it for this call
      // only so saved projects are byte-reproducible.
      const realIso = Date.prototype.toISOString;
      Date.prototype.toISOString = function () { return SAVED_AT_PINNED; };
      try { captureDownload(() => saveProject()); } finally { // editor.js:2550-2591
        Date.prototype.toISOString = realIso;
        URL.createObjectURL = realCreate;
      }
      if (!blob) throw new Error('saveProject made no blob');
      return blob.text();
    },

    // Every Word Art face loads from the vendored files, and Dancing Script's variable file draws
    // at wght 700 (its @font-face weight) rather than its 400 default.
    async selfCheck(faces, dancingUrl) {
      for (const f of faces) {
        const spec = `${f.weight} 64px "${f.family}"`;
        const got = await document.fonts.load(spec);
        if (!got.length || got.some((x) => x.status !== 'loaded')) throw new Error(`vendored face did not load: ${spec}`);
      }
      const probe = new FontFace('PrefDancing400', `url("${dancingUrl}")`, { weight: '400' });
      await probe.load();
      document.fonts.add(probe);
      const ctx = document.createElement('canvas').getContext('2d');
      ctx.font = '900 64px "Dancing Script"';
      const asServed = ctx.measureText('RIZZ').width;
      ctx.font = '400 64px "PrefDancing400"';
      const atDefault = ctx.measureText('RIZZ').width;
      document.fonts.delete(probe);
      if (asServed === atDefault) throw new Error('Dancing Script drew at wght 400: the @font-face weight 700 did not reach the variable axis');
      return faces.length;
    },
  };
}());
