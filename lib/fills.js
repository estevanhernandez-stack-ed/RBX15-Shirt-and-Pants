// Region fill rendering — solid, gradients, and patterns.
//
// A region fill is either a legacy color string (what v4.0 stored) or a
// descriptor object: { type, c1, c2, angle, scale }. normalizeFill() folds
// the string form into a solid descriptor so old saved projects and undo
// snapshots keep working untouched.
//
// paintRegionFill() draws into a region rect (clipped) on any 2D context —
// the same call feeds the on-screen render and the PNG export, so what you
// see is what you upload. Patterns are anchored to the region and camo is
// deterministically seeded, so a fill renders identically every frame and
// on export (no shimmer, no screen/export drift).
//
// Loaded via <script> in editor.html (window.RBX15Fills) and require() in
// tests. Dependency-free.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  }
  if (root) {
    root.RBX15Fills = factory();
  }
}(typeof self !== 'undefined' ? self : this, function () {

  const TYPES = ['solid', 'linear', 'radial', 'stripes', 'checker', 'dots', 'camo'];

  function normalizeFill(fill) {
    if (!fill) return { type: 'solid', c1: '#000000' };
    if (typeof fill === 'string') return { type: 'solid', c1: fill };
    return fill;
  }

  // ---- small hex helpers (accept #rgb or #rrggbb) ----
  function hexToRgb(hex) {
    let h = (hex || '#000000').replace('#', '');
    if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
    const n = parseInt(h, 16);
    return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
  }
  function rgbToHex(r, g, b) {
    const to = v => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0');
    return '#' + to(r) + to(g) + to(b);
  }
  function mix(a, b, t) {
    const x = hexToRgb(a), y = hexToRgb(b);
    return rgbToHex(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t);
  }

  // Deterministic PRNG (mulberry32) — same seed, same sequence, so camo is
  // identical on screen and export.
  function seeded(seed) {
    let s = seed >>> 0;
    return function () {
      s = (s + 0x6D2B79F5) >>> 0;
      let t = Math.imul(s ^ (s >>> 15), 1 | s);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function seedFrom(str) {
    let h = 2166136261;
    for (let i = 0; i < str.length; i++) { h ^= str.charCodeAt(i); h = Math.imul(h, 16777619); }
    return h >>> 0;
  }

  function paintRegionFill(ctx, region, fill) {
    const f = normalizeFill(fill);
    const { x, y, w, h } = region;
    const c1 = f.c1 || '#000000';
    const c2 = f.c2 || '#ffffff';
    const scale = Math.max(2, f.scale || 16);
    const angle = ((f.angle || 0) * Math.PI) / 180;

    ctx.save();
    ctx.beginPath();
    ctx.rect(x, y, w, h);
    ctx.clip();

    if (f.type === 'linear') {
      const cx = x + w / 2, cy = y + h / 2;
      const dx = Math.cos(angle) * w / 2, dy = Math.sin(angle) * h / 2;
      const g = ctx.createLinearGradient(cx - dx, cy - dy, cx + dx, cy + dy);
      g.addColorStop(0, c1); g.addColorStop(1, c2);
      ctx.fillStyle = g; ctx.fillRect(x, y, w, h);
    } else if (f.type === 'radial') {
      const cx = x + w / 2, cy = y + h / 2;
      const g = ctx.createRadialGradient(cx, cy, 0, cx, cy, Math.max(w, h) / 2);
      g.addColorStop(0, c1); g.addColorStop(1, c2);
      ctx.fillStyle = g; ctx.fillRect(x, y, w, h);
    } else if (f.type === 'stripes') {
      // Global lattice: rotate around the canvas origin and phase stripes by
      // absolute position, so the same fill on adjacent regions lines up.
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      ctx.rotate(angle);
      const period = scale * 2;
      // R aligned to a whole number of periods so the lattice is anchored at
      // global 0 (predictable phase, still continuous across regions).
      const R = Math.ceil(1000 / period) * period;
      for (let i = -R; i < R; i += period) ctx.fillRect(i, -R, scale, R * 2);
    } else if (f.type === 'checker') {
      // Global cell grid — parity from absolute cell index, so neighbouring
      // regions share the checker seamlessly.
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      const gx0 = Math.floor(x / scale), gx1 = Math.ceil((x + w) / scale);
      const gy0 = Math.floor(y / scale), gy1 = Math.ceil((y + h) / scale);
      for (let gy = gy0; gy < gy1; gy++) {
        for (let gx = gx0; gx < gx1; gx++) {
          if (((gx + gy) & 1) === 0) ctx.fillRect(gx * scale, gy * scale, scale, scale);
        }
      }
    } else if (f.type === 'dots') {
      // Dots on the global grid so they continue across region seams.
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      const r = scale * 0.3;
      const gx0 = Math.floor(x / scale), gx1 = Math.ceil((x + w) / scale);
      const gy0 = Math.floor(y / scale), gy1 = Math.ceil((y + h) / scale);
      for (let gy = gy0; gy <= gy1; gy++) {
        for (let gx = gx0; gx <= gx1; gx++) {
          ctx.beginPath();
          ctx.arc(gx * scale + scale / 2, gy * scale + scale / 2, r, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    } else if (f.type === 'camo') {
      // Seed blobs per GLOBAL cell (not per region), so the camo field is one
      // continuous pattern across every region sharing the fill — still
      // deterministic, so screen and export match.
      const tones = [c1, c2, mix(c1, c2, 0.5), mix(c1, '#000000', 0.35)];
      ctx.fillStyle = tones[0]; ctx.fillRect(x, y, w, h);
      const cell = scale * 2;
      const gx0 = Math.floor(x / cell) - 1, gx1 = Math.ceil((x + w) / cell) + 1;
      const gy0 = Math.floor(y / cell) - 1, gy1 = Math.ceil((y + h) / cell) + 1;
      for (let gy = gy0; gy < gy1; gy++) {
        for (let gx = gx0; gx < gx1; gx++) {
          const rand = seeded(seedFrom(c1 + c2 + scale + '_' + gx + '_' + gy));
          for (let b = 0; b < 2; b++) {
            ctx.fillStyle = tones[1 + ((gx + gy + b) % 3)];
            const bx = (gx + rand()) * cell, by = (gy + rand()) * cell;
            const br = scale * (0.6 + rand() * 1.1);
            ctx.beginPath();
            for (let k = 0; k < 4; k++) {
              const ox = (rand() - 0.5) * br, oy = (rand() - 0.5) * br;
              ctx.moveTo(bx + ox + br, by + oy);
              ctx.arc(bx + ox, by + oy, br * (0.6 + rand() * 0.4), 0, Math.PI * 2);
            }
            ctx.fill();
          }
        }
      }
    } else {
      // solid (and any unknown type)
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
    }

    ctx.restore();
  }

  // CSS approximation for the little region-button swatches. The canvas
  // render above is the source of truth; this just has to read right at
  // thumbnail size.
  function fillToCSS(fill) {
    const f = normalizeFill(fill);
    const c1 = f.c1 || '#000000';
    const c2 = f.c2 || '#ffffff';
    const scale = Math.max(2, f.scale || 16);
    const ang = f.angle || 0;
    switch (f.type) {
      case 'linear': return 'linear-gradient(' + ang + 'deg, ' + c1 + ', ' + c2 + ')';
      case 'radial': return 'radial-gradient(circle, ' + c1 + ', ' + c2 + ')';
      case 'stripes': return 'repeating-linear-gradient(' + ang + 'deg, ' + c1 + ' 0, ' + c1 + ' ' + scale + 'px, ' + c2 + ' ' + scale + 'px, ' + c2 + ' ' + (scale * 2) + 'px)';
      case 'checker': return 'repeating-conic-gradient(' + c1 + ' 0% 25%, ' + c2 + ' 0% 50%) 0 0 / ' + (scale * 2) + 'px ' + (scale * 2) + 'px';
      case 'dots': return 'radial-gradient(' + c2 + ' 30%, transparent 32%) 0 0 / ' + scale + 'px ' + scale + 'px, ' + c1;
      case 'camo': return 'linear-gradient(135deg, ' + mix(c1, c2, 0.5) + ', ' + c1 + ' 60%, ' + c2 + ')';
      default: return c1; // solid
    }
  }

  return { TYPES, normalizeFill, paintRegionFill, fillToCSS, mix };
}));
