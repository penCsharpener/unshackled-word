import { useCallback, useEffect, useRef, useState } from "react";

/**
 * One shared playback helper for vocabulary audio. `play(url)` stops whatever plays and starts the new URL; the
 * Audio element is created inside the click so iOS Safari allows it. Presigned URLs expire after an hour, so a
 * failed start reports `failed` instead of throwing.
 */
export function useAudioPlayer() {
  const audio = useRef<HTMLAudioElement | null>(null);
  const [playing, setPlaying] = useState(false);
  const [failed, setFailed] = useState(false);

  const stop = useCallback(() => {
    audio.current?.pause();
    audio.current = null;
    setPlaying(false);
  }, []);

  useEffect(() => stop, [stop]);

  const play = useCallback(
    (url: string) => {
      stop();
      setFailed(false);
      const element = new Audio(url);
      audio.current = element;
      element.onended = () => setPlaying(false);
      element.onerror = () => {
        setFailed(true);
        setPlaying(false);
      };
      setPlaying(true);
      element.play().catch(() => {
        setFailed(true);
        setPlaying(false);
      });
    },
    [stop],
  );

  return { play, stop, playing, failed };
}
