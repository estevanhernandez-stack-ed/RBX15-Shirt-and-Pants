// Erase-stroke math for the layer-based editor.
//
// The eraser draws its stroke in template space (585x559), but layers keep
// their own image pixels plus a transform (x, y, scale, rotation, flipH,
// flipV). Erasing means mapping the stroke through the INVERSE of the
// layer's render transform and punching it out of the layer image with
// destination-out compositing.
//
// Render transform (see drawLayerWithAdjustments in editor.js):
//   translate(cx, cy) -> rotate(r) -> scale(flip) -> drawImage at
//   (-w/2, -h/2, w, h)   where w = imgW * scale, cx = x + w/2.
//
// Implementation note: the stroke is first rasterized into a layer-sized
// mask with plain source-over, and the destination-out composite happens at
// identity CTM between equal-sized canvases. Compositing through a live
// transform is where 2D implementations disagree (@napi-rs/canvas 1.0.2
// drops transformed destination-out source pixels that originate outside
// the destination bounds), and the two-step form renders identically.
//
// Loaded two ways: <script> tag in editor.html (attaches window.RBX15Erase)
// and require() in node:test (module.exports). Keep it dependency-free.
(function (root, factory) {
  if (typeof module === 'object' && module.exports) {
    module.exports = factory();
  }
  if (root) {
    root.RBX15Erase = factory();
  }
}(typeof self !== 'undefined' ? self : this, function () {

  /**
   * Return a new canvas holding `source` with `strokeCanvas` erased from it.
   *
   * @param {CanvasImageSource} source   layer image (Image or canvas), natural size
   * @param {{x:number, y:number, scale:number, rotation:number,
   *          flipH:boolean, flipV:boolean}} t   layer transform, template space
   * @param {CanvasImageSource} strokeCanvas   template-sized canvas; any pixel
   *          with alpha > 0 is erased (proportionally to its alpha)
   * @param {(w:number, h:number) => HTMLCanvasElement} createCanvas   canvas
   *          factory — document.createElement in the renderer, @napi-rs/canvas
   *          createCanvas in tests
   */
  function applyEraseStroke(source, t, strokeCanvas, createCanvas) {
    const imgW = source.width;
    const imgH = source.height;

    const w = imgW * t.scale;
    const h = imgH * t.scale;
    const cx = t.x + w / 2;
    const cy = t.y + h / 2;
    const rad = (t.rotation || 0) * Math.PI / 180;

    // Step 1: rasterize the stroke into layer-image space through the
    // inverse of the render transform, composed left-to-right:
    // image-center <- unscale <- unflip <- unrotate <- untranslate
    const mask = createCanvas(imgW, imgH);
    const mctx = mask.getContext('2d');
    mctx.translate(imgW / 2, imgH / 2);
    mctx.scale(1 / t.scale, 1 / t.scale);
    mctx.scale(t.flipH ? -1 : 1, t.flipV ? -1 : 1);
    mctx.rotate(-rad);
    mctx.translate(-cx, -cy);
    mctx.drawImage(strokeCanvas, 0, 0);

    // Step 2: punch the mask out of the layer image at identity CTM.
    const out = createCanvas(imgW, imgH);
    const ctx = out.getContext('2d');
    ctx.drawImage(source, 0, 0);
    ctx.globalCompositeOperation = 'destination-out';
    ctx.drawImage(mask, 0, 0);
    ctx.globalCompositeOperation = 'source-over';
    return out;
  }

  return { applyEraseStroke };
}));
