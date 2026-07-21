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
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      ctx.translate(x + w / 2, y + h / 2);
      ctx.rotate(angle);
      const span = Math.ceil(w + h);
      for (let i = -span; i < span; i += scale * 2) ctx.fillRect(i, -span, scale, span * 2);
    } else if (f.type === 'checker') {
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      const cols = Math.ceil(w / scale), rows = Math.ceil(h / scale);
      for (let ry = 0; ry < rows; ry++) {
        for (let rx = 0; rx < cols; rx++) {
          if (((rx + ry) & 1) === 0) ctx.fillRect(x + rx * scale, y + ry * scale, scale, scale);
        }
      }
    } else if (f.type === 'dots') {
      ctx.fillStyle = c1; ctx.fillRect(x, y, w, h);
      ctx.fillStyle = c2;
      const r = scale * 0.3;
      for (let yy = scale / 2; yy < h + scale; yy += scale) {
        for (let xx = scale / 2; xx < w + scale; xx += scale) {
          ctx.beginPath(); ctx.arc(x + xx, y + yy, r, 0, Math.PI * 2); ctx.fill();
        }
      }
    } else if (f.type === 'camo') {
      const tones = [c1, c2, mix(c1, c2, 0.5), mix(c1, '#000000', 0.35)];
      ctx.fillStyle = tones[0]; ctx.fillRect(x, y, w, h);
      const rand = seeded(seedFrom(f.type + c1 + c2 + scale));
      const blobs = Math.max(6, Math.round((w * h) / (scale * scale * 3)));
      for (let i = 0; i < blobs; i++) {
        ctx.fillStyle = tones[1 + (i % 3)];
        const bx = x + rand() * w, by = y + rand() * h;
        const br = scale * (0.6 + rand() * 1.1);
        ctx.beginPath();
        // a lumpy blob: a few overlapping circles
        for (let k = 0; k < 4; k++) {
          const ox = (rand() - 0.5) * br, oy = (rand() - 0.5) * br;
          ctx.moveTo(bx + ox + br, by + oy);
          ctx.arc(bx + ox, by + oy, br * (0.6 + rand() * 0.4), 0, Math.PI * 2);
        }
        ctx.fill();
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
