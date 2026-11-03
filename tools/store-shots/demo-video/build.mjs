#!/usr/bin/env node
// Renders a scene manifest into a narrated MP4.
//
// Two ffmpeg passes: one H.264/AAC segment per scene, then a stream-copy
// concat. Hard cuts only. One entrance-effect family for the whole video.
//
//   node scripts/demo-video/build.mjs demo/manifest.json
//
// Voiceover is cache-first and provider-agnostic: an existing audio file for a
// scene is always used as-is and no TTS provider is ever called for it.

import { execFile } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { pathToFileURL } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, "../..");
const manifestPath = path.resolve(process.argv[2] ?? path.join(repoRoot, "demo/manifest.json"));
let ffmpeg = process.env.FFMPEG_PATH ?? "ffmpeg";
let ffprobe = process.env.FFPROBE_PATH ?? "ffprobe";

const MIN_SCENE_DURATION = 2.5;
const DEFAULT_SILENT_DURATION = 8;
// Any of these count as cached narration, whatever produced them.
const AUDIO_EXTENSIONS = ["mp3", "wav", "m4a", "aac", "opus", "flac", "ogg"];

function resolveFromManifest(value) {
  if (path.isAbsolute(value)) return value;
  if (/^(demo|artifacts|scripts)[\\/]/.test(value)) return path.resolve(repoRoot, value);
  return path.resolve(path.dirname(manifestPath), value);
}

function sceneAsset(scene) {
  const value = scene.src ?? scene.asset ?? scene.path ?? scene.source;
  if (!value) throw new Error(`Scene ${scene.id ?? "(unnamed)"} has no src/asset/path/source.`);
  return resolveFromManifest(value);
}

function sceneType(scene, asset) {
  if (scene.type === "still" || scene.type === "clip") return scene.type;
  return /\.(mp4|mov|m4v|webm|mkv|avi)$/i.test(asset) ? "clip" : "still";
}

function sceneNarration(scene) {
  return scene.narration ?? scene.voiceover ?? scene.script ?? "";
}

function slideNumber(scene, index) {
  const explicit = Number(scene.number ?? scene.slide);
  return Number.isInteger(explicit) && explicit > 0 ? explicit : index + 1;
}

function slideKey(scene, index) {
  return `slide_${String(slideNumber(scene, index)).padStart(2, "0")}`;
}

// Cache-first lookup. An explicit per-scene override wins; otherwise any
// known audio extension for this slide counts as already-narrated.
function findCachedAudio(scene, index, voiceoverDir) {
  const override = scene.audio ?? scene.voiceoverFile;
  if (override) {
    const resolved = resolveFromManifest(override);
    return existsSync(resolved) ? resolved : null;
  }
  const stem = path.join(voiceoverDir, slideKey(scene, index));
  for (const extension of AUDIO_EXTENSIONS) {
    const candidate = `${stem}.${extension}`;
    if (existsSync(candidate)) return candidate;
  }
  return null;
}

async function run(command, args, options = {}) {
  console.log(`\n${command} ${args.map((arg) => JSON.stringify(arg)).join(" ")}`);
  try {
    return await execFileAsync(command, args, { cwd: repoRoot, maxBuffer: 16 * 1024 * 1024, ...options });
  } catch (error) {
    if (error.stdout) process.stdout.write(error.stdout);
    if (error.stderr) process.stderr.write(error.stderr);
    throw error;
  }
}

async function findTool(command, fallbackName) {
  try {
    await run(command, ["-version"]);
    return command;
  } catch (initialError) {
    if (process.platform === "win32" && process.env.LOCALAPPDATA) {
      const fallback = path.join(process.env.LOCALAPPDATA, "Microsoft", "WinGet", "Links", fallbackName);
      if (existsSync(fallback)) {
        await run(fallback, ["-version"]);
        return fallback;
      }
    }
    throw new Error(`${command} was not found. Install ffmpeg or set FFMPEG_PATH/FFPROBE_PATH.`, { cause: initialError });
  }
}

async function mediaDuration(file) {
  const { stdout } = await run(ffprobe, [
    "-v", "error",
    "-show_entries", "format=duration",
    "-of", "default=noprint_wrappers=1:nokey=1",
    file,
  ]);
  const duration = Number(stdout.trim());
  if (!Number.isFinite(duration) || duration <= 0) throw new Error(`Could not determine duration of ${file}`);
  return duration;
}

async function loadLocalEnv() {
  for (const name of [".env.local", ".env"]) {
    const envPath = path.join(repoRoot, name);
    if (!existsSync(envPath)) continue;
    const source = await readFile(envPath, "utf8");
    for (const rawLine of source.split(/\r?\n/)) {
      const line = rawLine.trim();
      if (!line || line.startsWith("#")) continue;
      const match = line.match(/^(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/);
      if (!match || process.env[match[1]] !== undefined) continue;
      let value = match[2].trim();
      if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
        value = value.slice(1, -1);
      }
      process.env[match[1]] = value;
    }
  }
}

// Provider adapters live in scripts/demo-video/adapters/<name>.mjs and export
// { extension, isConfigured(env), synth({ text, voice, model, instructions, env }) }.
// A manifest may also name a path to its own adapter file.
async function loadTtsAdapter(voiceoverConfig) {
  const requested = voiceoverConfig.provider ?? process.env.DEMO_TTS_PROVIDER ?? "openai";
  if (requested === "none" || requested === false) return null;
  const specifier = requested.includes("/") || requested.includes("\\") || requested.endsWith(".mjs")
    ? pathToFileURL(resolveFromManifest(requested)).href
    : pathToFileURL(path.join(scriptDir, "adapters", `${requested}.mjs`)).href;
  try {
    const adapter = await import(specifier);
    if (typeof adapter.synth !== "function") throw new Error(`Adapter "${requested}" does not export synth().`);
    return { name: requested, extension: adapter.extension ?? "mp3", isConfigured: adapter.isConfigured ?? (() => true), synth: adapter.synth };
  } catch (error) {
    throw new Error(`Could not load TTS adapter "${requested}": ${error.message}`);
  }
}

async function generateVoiceover({ adapter, scene, index, voiceoverDir, voiceoverConfig, manifest }) {
  const text = sceneNarration(scene);
  if (!text) return null;
  const destination = path.join(voiceoverDir, `${slideKey(scene, index)}.${adapter.extension}`);
  const audio = await adapter.synth({
    text,
    voice: scene.voice ?? voiceoverConfig.voice ?? manifest.voice,
    model: voiceoverConfig.model ?? manifest.ttsModel,
    instructions: scene.voiceInstructions ?? voiceoverConfig.instructions ?? manifest.voiceInstructions,
    env: process.env,
  });
  if (!audio) return null;
  await writeFile(destination, Buffer.isBuffer(audio) ? audio : Buffer.from(audio));
  console.log(`Generated ${path.relative(repoRoot, destination)} via ${adapter.name}`);
  return destination;
}

function evenNumber(value) {
  const rounded = Math.round(value);
  return rounded % 2 === 0 ? rounded : rounded - 1;
}

function fitFilters({ inputLabel, type, duration, effect, trimStart, geometry }) {
  const { width, height, fps, contentWidth, contentHeight, entranceDelay, entranceSeconds, blurSigma } = geometry;
  const durationText = duration.toFixed(3);
  const timeBase = type === "clip"
    // tpad clones the final frame so a short clip fills its scene instead of ending early.
    ? `trim=start=${trimStart.toFixed(3)},setpts=PTS-STARTPTS,fps=${fps},tpad=stop_mode=clone:stop_duration=${durationText},trim=duration=${durationText}`
    : `trim=duration=${durationText},setpts=PTS-STARTPTS,fps=${fps}`;
  const shared = [
    `[${inputLabel}]${timeBase},split=2[bgsrc][fgsrc]`,
    // The background is the same frame, blown up, blurred and darkened, so any
    // aspect ratio fills the canvas without letterbox bars.
    `[bgsrc]scale=${width}:${height}:force_original_aspect_ratio=increase,crop=${width}:${height},gblur=sigma=${blurSigma},eq=brightness=-0.08,setsar=1[bg]`,
    `[fgsrc]scale=${contentWidth}:${contentHeight}:force_original_aspect_ratio=decrease:force_divisible_by=2,setsar=1[fg]`,
  ];
  // Ease-out cubic: p = 1 - (1-u)^3 over the entrance window.
  const u = `min(max((t-${entranceDelay.toFixed(2)})/${entranceSeconds.toFixed(2)},0),1)`;
  const p = `(1-pow(1-${u},3))`;
  if (effect === "slide_left") {
    shared.push(`[bg][fg]overlay=x='-w+((W-w)/2+w)*${p}':y='(H-h)/2':eval=frame:shortest=1,format=yuv420p[v]`);
  } else if (effect === "fade") {
    shared[2] = `[fgsrc]scale=${contentWidth}:${contentHeight}:force_original_aspect_ratio=decrease:force_divisible_by=2,setsar=1,format=rgba,fade=t=in:st=${entranceDelay.toFixed(2)}:d=${entranceSeconds.toFixed(2)}:alpha=1[fg]`;
    shared.push(`[bg][fg]overlay=x='(W-w)/2':y='(H-h)/2':eval=frame:shortest=1,format=yuv420p[v]`);
  } else {
    shared.push(`[bg][fg]overlay=x='(W-w)/2':y='H+((H-h)/2-H)*${p}':eval=frame:shortest=1,format=yuv420p[v]`);
  }
  return shared;
}

function audioFilter(duration, sampleRate) {
  const durationText = duration.toFixed(3);
  // Trim, pad to the full scene length, then trim again so padding can never overrun.
  return `[1:a]atrim=0:${durationText},apad=whole_dur=${durationText},atrim=0:${durationText},aresample=${sampleRate},aformat=sample_rates=${sampleRate}:channel_layouts=stereo[a]`;
}

async function renderSegment({ scene, index, asset, type, audio, duration, effect, trimStart, destination, encode, geometry }) {
  const visualInput = type === "still"
    ? ["-loop", "1", "-framerate", String(geometry.fps), "-i", asset]
    : ["-i", asset];
  const audioInput = audio
    ? ["-i", audio]
    : ["-f", "lavfi", "-i", `anullsrc=r=${encode.sampleRate}:cl=stereo`];
  const filters = [
    ...fitFilters({ inputLabel: "0:v", type, duration, effect, trimStart, geometry }),
    audioFilter(duration, encode.sampleRate),
  ].join(";");
  await run(ffmpeg, [
    "-y", ...visualInput, ...audioInput,
    "-filter_complex", filters,
    "-map", "[v]", "-map", "[a]",
    "-t", duration.toFixed(3),
    "-r", String(geometry.fps),
    "-c:v", encode.videoCodec, "-preset", encode.preset, "-crf", String(encode.crf), "-pix_fmt", encode.pixelFormat,
    "-c:a", encode.audioCodec, "-b:a", encode.audioBitrate, "-ar", String(encode.sampleRate), "-ac", "2",
    "-movflags", "+faststart",
    destination,
  ]);
  console.log(`Rendered scene ${index + 1}: ${scene.title ?? scene.id ?? path.basename(asset)} (${duration.toFixed(2)}s)`);
}

async function main() {
  ffmpeg = await findTool(ffmpeg, "ffmpeg.exe");
  ffprobe = await findTool(ffprobe, "ffprobe.exe");
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  const scenes = Array.isArray(manifest) ? manifest : manifest.scenes;
  if (!Array.isArray(scenes) || scenes.length === 0) throw new Error("Manifest must contain a non-empty scenes array.");

  const output = typeof manifest.output === "object" ? manifest.output : {};
  const width = evenNumber(Number(output.width ?? 1920));
  const height = evenNumber(Number(output.height ?? 1080));
  const fps = Number(output.fps ?? 30);
  const style = manifest.visual_style ?? {};
  const contentScale = Number(style.content_scale ?? 0.92);
  const geometry = {
    width,
    height,
    fps,
    contentWidth: evenNumber(width * contentScale),
    contentHeight: evenNumber(height * contentScale),
    entranceDelay: Number(style.entrance_delay ?? 0.1),
    entranceSeconds: Number(style.entrance_seconds ?? 0.45),
    blurSigma: Number(style.background_blur ?? 28),
  };
  const encode = {
    videoCodec: output.video_codec ?? "libx264",
    preset: output.preset ?? "medium",
    crf: Number(output.crf ?? 20),
    pixelFormat: output.pixel_format ?? "yuv420p",
    audioCodec: output.audio_codec ?? "aac",
    audioBitrate: output.audio_bitrate ?? "160k",
    sampleRate: Number(output.audio_sample_rate ?? 44100),
  };
  const runtimeWarning = Number(manifest.runtime_warning_seconds ?? 175);

  const outputPath = resolveFromManifest(output.path ?? manifest.output ?? "out/demo.mp4");
  const voiceoverConfig = typeof manifest.voiceover === "object" ? manifest.voiceover : {};
  const voiceoverDir = resolveFromManifest(voiceoverConfig.directory ?? manifest.voiceoverDir ?? "voiceover");
  const workDir = path.join(path.dirname(outputPath), ".segments");

  const requestedEffects = new Set(scenes.map((scene) => scene.effect).filter(Boolean));
  const effect = style.effect_family ?? manifest.entranceEffect ?? [...requestedEffects][0] ?? "slide_up";
  if (!["slide_up", "slide_left", "fade"].includes(effect)) throw new Error(`Unsupported entrance effect: ${effect}`);
  if (requestedEffects.size > 1 || (requestedEffects.size === 1 && !requestedEffects.has(effect))) {
    throw new Error("Use one entrance effect family for the entire video. Variety reads as chaos.");
  }

  await mkdir(path.dirname(outputPath), { recursive: true });
  await mkdir(voiceoverDir, { recursive: true });
  await rm(workDir, { recursive: true, force: true });
  await mkdir(workDir, { recursive: true });
  await loadLocalEnv();

  const adapter = await loadTtsAdapter(voiceoverConfig);
  const canSynthesize = Boolean(adapter && adapter.isConfigured(process.env));
  if (!canSynthesize) {
    console.log(adapter
      ? `TTS provider "${adapter.name}" is not configured; using cached audio where present, silence elsewhere.`
      : "No TTS provider selected; using cached audio where present, silence elsewhere.");
  }

  const durations = {};
  const prepared = [];
  for (const [index, scene] of scenes.entries()) {
    const asset = sceneAsset(scene);
    if (!existsSync(asset)) throw new Error(`Missing scene asset: ${asset}`);

    // Cache-first: an existing file always wins and never triggers a provider call.
    let audio = findCachedAudio(scene, index, voiceoverDir);
    if (!audio && canSynthesize) {
      audio = await generateVoiceover({ adapter, scene, index, voiceoverDir, voiceoverConfig, manifest });
    }

    const narrationDuration = audio ? await mediaDuration(audio) : null;
    const explicitDuration = Number(scene.duration);
    const hasExplicit = Number.isFinite(explicitDuration) && explicitDuration > 0;
    let duration = hasExplicit
      ? explicitDuration
      : narrationDuration !== null
        ? Math.max(narrationDuration + 0.35, MIN_SCENE_DURATION)
        : Number(manifest.defaultDuration ?? DEFAULT_SILENT_DURATION);
    // The opening scene gets a beat of extra air before the narration lands.
    if (index === 0 && !hasExplicit) duration += 0.5;
    if (narrationDuration !== null && duration + 0.001 < narrationDuration) {
      throw new Error(`Scene ${index + 1} duration (${duration}s) is shorter than its narration (${narrationDuration.toFixed(3)}s). Raise duration or cut words.`);
    }

    durations[slideKey(scene, index)] = {
      audio: narrationDuration === null ? null : Number(narrationDuration.toFixed(3)),
      scene: Number(duration.toFixed(3)),
      source: audio ? path.relative(repoRoot, audio) : "silent",
    };
    const trimStartValue = Number(scene.trim_start ?? scene.capture?.trim_start ?? 0);
    const trimStart = Number.isFinite(trimStartValue) && trimStartValue >= 0 ? trimStartValue : 0;
    prepared.push({ scene, index, asset, type: sceneType(scene, asset), audio, duration, trimStart });
  }
  await writeFile(path.join(voiceoverDir, "durations.json"), `${JSON.stringify(durations, null, 2)}\n`);

  const totalDuration = prepared.reduce((sum, item) => sum + item.duration, 0);
  console.log(`\nPlanned runtime: ${Math.floor(totalDuration / 60)}:${String(Math.round(totalDuration % 60)).padStart(2, "0")}`);
  if (totalDuration > runtimeWarning) {
    console.warn(`WARNING: planned runtime ${totalDuration.toFixed(1)}s exceeds ${runtimeWarning}s.`);
  }

  const segments = [];
  for (const item of prepared) {
    const destination = path.join(workDir, `segment_${String(item.index + 1).padStart(2, "0")}.mp4`);
    await renderSegment({ ...item, effect, destination, encode, geometry });
    segments.push(destination);
  }

  const concatPath = path.join(workDir, "concat.txt");
  const concatFile = segments.map((file) => `file '${file.replaceAll("'", "'\\''").replaceAll("\\", "/")}'`).join("\n");
  await writeFile(concatPath, `${concatFile}\n`);
  await run(ffmpeg, [
    "-y", "-f", "concat", "-safe", "0", "-i", concatPath,
    "-c", "copy", "-movflags", "+faststart", outputPath,
  ]);
  console.log(`\nWrote ${path.relative(repoRoot, outputPath)} (${totalDuration.toFixed(1)}s, ${width}x${height}, ${fps}fps)`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
