// ElevenLabs TTS adapter.
//
// Same contract as openai.mjs:
//   extension            file extension the provider returns, without a dot
//   isConfigured(env)    cheap check; false means "render silent, do not throw"
//   synth({...})         returns a Buffer of audio, or null to skip
//
// `voice` is an ElevenLabs voice ID (the manifest's voiceover.voice, or ELEVENLABS_VOICE_ID). `model` defaults to
// eleven_multilingual_v2. The key is ELEVENLABS_API_KEY, from the environment or .env.local, never the manifest.

export const extension = "mp3";

export function isConfigured(env) {
  return Boolean(env.ELEVENLABS_API_KEY);
}

export async function synth({ text, voice, model, env }) {
  const voiceId = voice ?? env.ELEVENLABS_VOICE_ID;
  if (!voiceId) throw new Error("ElevenLabs TTS needs a voice ID: set voiceover.voice in the manifest or ELEVENLABS_VOICE_ID.");
  const response = await fetch(
    `https://api.elevenlabs.io/v1/text-to-speech/${encodeURIComponent(voiceId)}?output_format=mp3_44100_128`,
    {
      method: "POST",
      headers: {
        "xi-api-key": env.ELEVENLABS_API_KEY,
        "Content-Type": "application/json",
        Accept: "audio/mpeg",
      },
      body: JSON.stringify({
        text,
        model_id: model ?? env.ELEVENLABS_MODEL ?? "eleven_multilingual_v2",
        voice_settings: { stability: 0.45, similarity_boost: 0.8, style: 0.25, use_speaker_boost: true },
      }),
    },
  );
  if (!response.ok) {
    // A configured provider that fails is a real error: fail loudly rather than ship a silent video that looks finished.
    throw new Error(`ElevenLabs TTS failed: ${response.status} ${await response.text()}`);
  }
  return Buffer.from(await response.arrayBuffer());
}
