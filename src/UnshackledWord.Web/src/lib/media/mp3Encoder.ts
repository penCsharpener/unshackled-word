// MP3 encoding core (ported from Lexio 0029.3f): a pure Float32 to MP3 function without DOM or state.
// lamejs is passed in so the library stays out of the first-paint bundle (the recorder imports it on demand).

export const MP3_SAMPLE_RATE = 22_050;
export const MP3_BITRATE_KBPS = 64;
/** Hard cap of a take; a pronunciation is two seconds, this only stops a forgotten open microphone. */
export const MAX_RECORDING_SECONDS = 30;

export type LamejsModule = typeof import("@breezystack/lamejs");

/** Joins the frames the worklet delivered into one buffer. */
export function mergeFrames(frames: Float32Array[]): Float32Array {
  const merged = new Float32Array(frames.reduce((n, f) => n + f.length, 0));
  let pos = 0;
  for (const frame of frames) {
    merged.set(frame, pos);
    pos += frame.length;
  }
  return merged;
}

/** Encodes mono `samples` at `sampleRate` to MP3 bytes (Int16 PCM in bounded chunks, then flushed). */
export function encodeMp3(
  samples: Float32Array,
  sampleRate: number,
  lamejs: LamejsModule,
): Uint8Array {
  const encoder = new lamejs.Mp3Encoder(1, sampleRate, MP3_BITRATE_KBPS);
  const pcm = new Int16Array(samples.length);
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i] ?? 0));
    pcm[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
  }
  const chunks: Uint8Array[] = [];
  const push = (encoded: Int8Array | Uint8Array): void => {
    if (encoded.length > 0) {
      chunks.push(new Uint8Array(encoded.buffer, encoded.byteOffset, encoded.byteLength));
    }
  };
  const chunkSamples = 1152 * 100; // 1152 samples per MPEG granule
  for (let offset = 0; offset < pcm.length; offset += chunkSamples) {
    push(encoder.encodeBuffer(pcm.subarray(offset, Math.min(offset + chunkSamples, pcm.length))));
  }
  push(encoder.flush());
  const out = new Uint8Array(chunks.reduce((n, c) => n + c.length, 0));
  let pos = 0;
  for (const chunk of chunks) {
    out.set(chunk, pos);
    pos += chunk.length;
  }
  return out;
}
