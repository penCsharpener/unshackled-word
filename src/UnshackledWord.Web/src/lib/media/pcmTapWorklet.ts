// AudioWorklet processor (ported from Lexio 0029.5f): posts each render quantum as a Float32 frame. The node is
// mono and the AudioContext runs at the MP3 target rate, so the frames are what the encoder wants.
// Built as its own bundle through `?worker&url`.

// Minimal declarations: the DOM lib does not carry the AudioWorkletGlobalScope types.
declare class AudioWorkletProcessor {
  port: MessagePort;
}
declare function registerProcessor(
  name: string,
  processorCtor: new () => AudioWorkletProcessor,
): void;

class PcmTap extends AudioWorkletProcessor {
  process(inputs: Float32Array[][]): boolean {
    const channel = inputs[0]?.[0];
    if (channel) this.port.postMessage(channel.slice(0));
    return true;
  }
}

registerProcessor("pcm-tap", PcmTap);
