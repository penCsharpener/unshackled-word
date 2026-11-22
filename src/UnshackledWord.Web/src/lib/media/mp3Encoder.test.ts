import * as lamejs from "@breezystack/lamejs";
import { describe, expect, it } from "vitest";
import { encodeMp3, MP3_SAMPLE_RATE, mergeFrames } from "./mp3Encoder";

describe("mp3Encoder", () => {
  it("joins frames in order", () => {
    const merged = mergeFrames([new Float32Array([1, 2]), new Float32Array([3])]);
    expect(Array.from(merged)).toEqual([1, 2, 3]);
  });

  it("encodes a short sine to an MP3 with a frame sync or ID3 header", () => {
    const samples = new Float32Array(MP3_SAMPLE_RATE / 2).map((_, i) =>
      Math.sin((2 * Math.PI * 440 * i) / MP3_SAMPLE_RATE),
    );
    const mp3 = encodeMp3(samples, MP3_SAMPLE_RATE, lamejs);
    expect(mp3.length).toBeGreaterThan(0);
    expect(mp3.length).toBeLessThan(1024 * 1024);
    const frameSync = mp3[0] === 0xff && ((mp3[1] ?? 0) & 0xe0) === 0xe0;
    const id3 = mp3[0] === 0x49 && mp3[1] === 0x44 && mp3[2] === 0x33;
    expect(frameSync || id3).toBe(true);
  });
});
