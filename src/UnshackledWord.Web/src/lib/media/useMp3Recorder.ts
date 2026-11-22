import { useCallback, useEffect, useRef, useState } from "react";
import { encodeMp3, MAX_RECORDING_SECONDS, MP3_SAMPLE_RATE, mergeFrames } from "./mp3Encoder";
import pcmTapUrl from "./pcmTapWorklet?worker&url";

export type RecorderPhase = "idle" | "recording" | "encoding";
export type RecorderError = "unsupported" | "denied" | "noDevice" | "other";

/**
 * Browser recording (ported from Lexio 0029.5f): getUserMedia, an AudioContext at the MP3 rate (created and resumed
 * inside the click, iOS needs that), an AudioWorklet that taps mono PCM, then lamejs encodes once on stop. No
 * MediaRecorder, so the result plays on iOS Safari. `stop()` resolves with the MP3 blob (null when nothing came out).
 */
export function useMp3Recorder() {
  const [phase, setPhase] = useState<RecorderPhase>("idle");
  const [error, setError] = useState<RecorderError | null>(null);
  const [elapsed, setElapsed] = useState(0);
  const parts = useRef<{
    stream: MediaStream;
    context: AudioContext;
    source: MediaStreamAudioSourceNode;
    tap: AudioWorkletNode;
    frames: Float32Array[];
    timer: ReturnType<typeof setInterval>;
    auto?: () => void;
  } | null>(null);

  const teardown = useCallback(() => {
    const p = parts.current;
    parts.current = null;
    if (!p) return;
    clearInterval(p.timer);
    for (const track of p.stream.getTracks()) track.stop();
    p.source.disconnect();
    p.tap.disconnect();
    void p.context.close();
  }, []);

  useEffect(() => teardown, [teardown]);

  const finish = useCallback(async (): Promise<Blob | null> => {
    const p = parts.current;
    if (!p) return null;
    const frames = p.frames;
    teardown();
    setPhase("encoding");
    try {
      const lamejs = await import("@breezystack/lamejs");
      const bytes = encodeMp3(mergeFrames(frames), MP3_SAMPLE_RATE, lamejs);
      return bytes.length > 0
        ? new Blob([bytes.buffer as ArrayBuffer], { type: "audio/mpeg" })
        : null;
    } catch {
      setError("other");
      return null;
    } finally {
      setPhase("idle");
    }
  }, [teardown]);

  /** `onAutoStop` receives the take when the hard cap ends it. */
  const start = useCallback(
    async (onAutoStop?: (mp3: Blob | null) => void) => {
      setError(null);
      if (!navigator.mediaDevices?.getUserMedia) {
        setError("unsupported");
        return;
      }
      let stream: MediaStream;
      try {
        stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      } catch (err) {
        const name = (err as DOMException)?.name;
        setError(
          name === "NotAllowedError" ? "denied" : name === "NotFoundError" ? "noDevice" : "other",
        );
        return;
      }
      try {
        const context = new AudioContext({ sampleRate: MP3_SAMPLE_RATE });
        if (context.state !== "running") await context.resume();
        await context.audioWorklet.addModule(pcmTapUrl);
        const source = context.createMediaStreamSource(stream);
        const tap = new AudioWorkletNode(context, "pcm-tap", {
          numberOfOutputs: 0,
          channelCount: 1,
          channelCountMode: "explicit",
        });
        const frames: Float32Array[] = [];
        tap.port.onmessage = (event) => frames.push(event.data as Float32Array);
        source.connect(tap);
        let seconds = 0;
        const timer = setInterval(() => {
          seconds += 1;
          setElapsed(seconds);
          if (seconds >= MAX_RECORDING_SECONDS) void finish().then((mp3) => onAutoStop?.(mp3));
        }, 1000);
        parts.current = { stream, context, source, tap, frames, timer };
        setElapsed(0);
        setPhase("recording");
      } catch {
        for (const track of stream.getTracks()) track.stop();
        setError("other");
      }
    },
    [finish],
  );

  return { phase, error, elapsed, start, stop: finish };
}
