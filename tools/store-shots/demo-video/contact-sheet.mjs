#!/usr/bin/env node
// Builds QA artifacts for a rendered video: a tiled contact sheet, plus
// full-resolution frame extracts at specific timestamps.
//
//   node scripts/demo-video/contact-sheet.mjs demo/out/demo.mp4
//   node scripts/demo-video/contact-sheet.mjs demo/out/demo.mp4 --frames 5,60,115
//
// Look at the sheet before uploading. It catches the failures that are obvious
// to an eye and invisible to a script: a scene that never loaded, a modal left
// open, a blank frame where a clip ran short.

import { execFile } from "node:child_process";
import { existsSync } from "node:fs";
import { mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);
const args = process.argv.slice(2);
const valueAfter = (flag) => {
  const index = args.indexOf(flag);
  return index >= 0 ? args[index + 1] : undefined;
};

const videoPath = path.resolve(args[0] && !args[0].startsWith("--") ? args[0] : "demo/out/demo.mp4");
const columns = Number(valueAfter("--columns") ?? 6);
const rows = Number(valueAfter("--rows") ?? 4);
const tileWidth = Number(valueAfter("--tile-width") ?? 400);
const frameList = (valueAfter("--frames") ?? "")
  .split(",")
  .map((value) => Number(value.trim()))
  .filter((value) => Number.isFinite(value) && value >= 0);

let ffmpeg = process.env.FFMPEG_PATH ?? "ffmpeg";
let ffprobe = process.env.FFPROBE_PATH ?? "ffprobe";

async function run(command, commandArgs) {
  try {
    return await execFileAsync(command, commandArgs, { maxBuffer: 16 * 1024 * 1024 });
  } catch (error) {
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
      if (existsSync(fallback)) return fallback;
    }
    throw new Error(`${command} was not found. Install ffmpeg or set FFMPEG_PATH/FFPROBE_PATH.`, { cause: initialError });
  }
}

async function main() {
  if (!existsSync(videoPath)) throw new Error(`No such video: ${videoPath}`);
  ffmpeg = await findTool(ffmpeg, "ffmpeg.exe");
  ffprobe = await findTool(ffprobe, "ffprobe.exe");

  const outDir = path.dirname(videoPath);
  await mkdir(outDir, { recursive: true });

  const { stdout } = await run(ffprobe, [
    "-v", "error",
    "-show_entries", "format=duration",
    "-of", "default=noprint_wrappers=1:nokey=1",
    videoPath,
  ]);
  const duration = Number(stdout.trim());
  if (!Number.isFinite(duration) || duration <= 0) throw new Error(`Could not read duration of ${videoPath}`);

  // Extract each tile at an explicit timestamp rather than letting an fps
  // filter sample the stream. The filter approach silently drops tiles and
  // lands on frames that do not match the times it claims, which makes the
  // sheet lie about the video. Seek-per-frame is slower and exact.
  const tiles = columns * rows;
  const interval = duration / tiles;
  const stagingDir = path.join(outDir, ".contact");
  await rm(stagingDir, { recursive: true, force: true });
  await mkdir(stagingDir, { recursive: true });

  for (let index = 0; index < tiles; index += 1) {
    // Sample mid-segment so tiles miss both the entrance animation and the cut.
    const timestamp = (index + 0.5) * interval;
    await run(ffmpeg, [
      "-y", "-v", "error",
      "-ss", timestamp.toFixed(3),
      "-i", videoPath,
      "-frames:v", "1",
      "-vf", `scale=${tileWidth}:-2`,
      path.join(stagingDir, `tile_${String(index + 1).padStart(3, "0")}.png`),
    ]);
  }

  const sheetPath = path.join(outDir, "contact-sheet.png");
  await run(ffmpeg, [
    "-y", "-v", "error",
    "-framerate", "1",
    "-i", path.join(stagingDir, "tile_%03d.png"),
    "-vf", `tile=${columns}x${rows}`,
    "-frames:v", "1",
    sheetPath,
  ]);
  await rm(stagingDir, { recursive: true, force: true });
  console.log(`Wrote ${path.relative(process.cwd(), sheetPath)} (${columns}x${rows}, one frame every ${interval.toFixed(1)}s)`);

  for (const seconds of frameList) {
    if (seconds > duration) {
      console.warn(`Skipping frame at ${seconds}s: past the end of a ${duration.toFixed(1)}s video.`);
      continue;
    }
    const framePath = path.join(outDir, `frame-${String(Math.round(seconds)).padStart(3, "0")}.png`);
    await run(ffmpeg, ["-y", "-ss", String(seconds), "-i", videoPath, "-frames:v", "1", framePath]);
    console.log(`Wrote ${path.relative(process.cwd(), framePath)}`);
  }

  console.log(`\nRuntime ${Math.floor(duration / 60)}:${String(Math.round(duration % 60)).padStart(2, "0")}. Open the contact sheet before you upload.`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
