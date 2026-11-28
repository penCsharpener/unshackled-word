import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, describeApiError } from "@/lib/api";
import { useMp3Recorder } from "@/lib/media/useMp3Recorder";
import { useAudioPlayer } from "@/lib/vocab/useAudioPlayer";
import { Button, buttonClasses } from "./Button";
import { FormError } from "./FormError";

/** Server limit of an uploaded or recorded take. */
export const AUDIO_MAX_BYTES = 1024 * 1024;

// Stroked 24x24 icon paths (Lucide style).
const MICROPHONE_ICON_PATHS = [
  "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3Z",
  "M19 10v2a7 7 0 0 1-14 0v-2",
  "M12 19v3",
] as const;
const UPLOAD_ICON_PATHS = [
  "M12 15V3",
  "m17 8-5-5-5 5",
  "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4",
] as const;

function ActionIcon({ paths }: { paths: readonly string[] }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths.map((d) => (
        <path key={d} d={d} />
      ))}
    </svg>
  );
}

export interface AudioFieldProps {
  /** Presigned URL of the saved audio, null when there is none. */
  src: string | null;
  /** Reader view: one play Button, nothing else. */
  readOnly?: boolean;
  /** MP3 picked from disk (`accept="audio/mpeg"`, at most 1 MB, checked here as well). */
  onUpload?: (file: File) => Promise<void>;
  /** Recorded MP3 the maintainer chose to keep. */
  onRecord?: (mp3: Blob) => Promise<void>;
  onDelete?: () => Promise<void>;
}

const clock = (seconds: number) =>
  `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;

/**
 * Audio of a vocabulary entry. Read-only it is one play button; editable it walks through empty, ready, recording,
 * preview and uploading (roadmap 0022.6f). The recorder encodes MP3 in the browser so iOS can play the result.
 */
export function AudioField({
  src,
  readOnly = false,
  onUpload,
  onRecord,
  onDelete,
}: AudioFieldProps) {
  const { t } = useTranslation();
  const player = useAudioPlayer();
  const recorder = useMp3Recorder();
  const [take, setTake] = useState<{ blob: Blob; url: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const takeUrl = useRef<string | null>(null);

  useEffect(() => {
    takeUrl.current = take?.url ?? null;
  }, [take]);
  useEffect(
    () => () => {
      if (takeUrl.current) URL.revokeObjectURL(takeUrl.current);
    },
    [],
  );

  const playButton = (size: "sm" | "md", source: string) => (
    <Button
      variant="secondary"
      size={size}
      aria-label={t("vocab.audio.play")}
      aria-busy={player.playing}
      onClick={() => player.play(source)}
    >
      ▶ {t("vocab.audio.listen")}
    </Button>
  );

  if (readOnly) {
    if (!src) return null;
    return (
      <div className="flex items-center gap-2">
        {playButton("md", src)}
        <FormError message={player.failed ? t("vocab.audio.playFailed") : ""} />
      </div>
    );
  }

  const fail = (err: unknown) =>
    setError(
      err instanceof ApiError && err.status === 503
        ? t("vocab.audio.notConfigured")
        : describeApiError(err, t("vocab.audio.failed")),
    );

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (err) {
      fail(err);
    } finally {
      setBusy(false);
    }
  };

  const setTakeBlob = (blob: Blob | null) => {
    setTake((old) => {
      if (old) URL.revokeObjectURL(old.url);
      return blob ? { blob, url: URL.createObjectURL(blob) } : null;
    });
  };

  const onPick = (file: File | undefined) => {
    if (!file || !onUpload) return;
    if (file.size > AUDIO_MAX_BYTES || (file.type && file.type !== "audio/mpeg")) {
      setError(t("vocab.audio.limits"));
      return;
    }
    void run(() => onUpload(file));
  };

  const startRecording = () => {
    setError("");
    player.stop();
    void recorder.start((mp3) => setTakeBlob(mp3));
  };

  const stopRecording = async () => setTakeBlob(await recorder.stop());

  const keep = () => {
    const blob = take?.blob;
    if (!blob || !onRecord) return;
    void run(async () => {
      await onRecord(blob);
      setTakeBlob(null);
    });
  };

  const recorderError = recorder.error ? t(`vocab.audio.recorder.${recorder.error}`) : "";
  const recordLabel = src ? t("vocab.audio.rerecord") : t("vocab.audio.record");
  const recordButton = onRecord ? (
    <Button
      size="sm"
      disabled={busy}
      onClick={startRecording}
      aria-label={recordLabel}
      title={recordLabel}
    >
      <ActionIcon paths={MICROPHONE_ICON_PATHS} />
    </Button>
  ) : null;

  let body: React.ReactNode;
  if (busy) {
    body = (
      <p role="status" className="text-sm text-fg-muted">
        {t("vocab.audio.uploading")}
      </p>
    );
  } else if (recorder.phase === "recording") {
    body = (
      <>
        <p role="status" className="flex items-center gap-2 text-sm">
          <span className="h-3 w-3 animate-pulse rounded-full bg-sev-error" />
          {t("vocab.audio.recording", { time: clock(recorder.elapsed) })}
        </p>
        <div className="flex gap-2">
          <Button variant="primary" size="sm" onClick={() => void stopRecording()}>
            ■ {t("vocab.audio.stop")}
          </Button>
        </div>
      </>
    );
  } else if (recorder.phase === "encoding") {
    body = (
      <p role="status" className="text-sm text-fg-muted">
        {t("vocab.audio.encoding")}
      </p>
    );
  } else if (take) {
    body = (
      <>
        <p className="text-sm">
          {t("vocab.audio.take", { size: Math.max(1, Math.round(take.blob.size / 1024)) })}
        </p>
        <div className="flex flex-wrap gap-2">
          {playButton("sm", take.url)}
          <Button variant="primary" size="sm" onClick={keep}>
            {t("vocab.audio.keep")}
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setTakeBlob(null)}>
            {t("vocab.audio.discard")}
          </Button>
        </div>
      </>
    );
  } else if (src) {
    body = (
      <div className="flex flex-wrap items-center gap-2">
        {playButton("sm", src)}
        <span className="text-sm text-fg-muted">{t("vocab.audio.saved")}</span>
        {recordButton}
        {onDelete ? (
          <Button variant="ghost" size="sm" onClick={() => void run(onDelete)}>
            {t("vocab.audio.delete")}
          </Button>
        ) : null}
      </div>
    );
  } else {
    body = (
      <>
        <p className="text-sm text-fg-muted">{t("vocab.audio.none")}</p>
        <div className="flex flex-wrap gap-2">
          {recordButton}
          {onUpload ? (
            <label
              title={t("vocab.audio.upload")}
              className={`${buttonClasses({ variant: "secondary", size: "sm" })} cursor-pointer focus-within:ring-2 focus-within:ring-accent`}
            >
              <ActionIcon paths={UPLOAD_ICON_PATHS} />
              <span className="sr-only">{t("vocab.audio.upload")}</span>
              <input
                type="file"
                accept="audio/mpeg"
                className="sr-only"
                onChange={(event) => {
                  onPick(event.target.files?.[0]);
                  event.target.value = "";
                }}
              />
            </label>
          ) : null}
        </div>
        <p className="text-xs text-fg-muted">{t("vocab.audio.limits")}</p>
      </>
    );
  }

  return (
    <div className="grid gap-2">
      {body}
      <FormError
        message={error || recorderError || (player.failed ? t("vocab.audio.playFailed") : "")}
      />
    </div>
  );
}
