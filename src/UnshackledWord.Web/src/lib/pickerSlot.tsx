import { createContext, type ReactNode, useContext, useState } from "react";

/** The empty container in the middle of the header; from 1200px the chapter picker is rendered into it. */
const SlotContext = createContext<HTMLElement | null>(null);
const SetSlotContext = createContext<(element: HTMLElement | null) => void>(() => {});

export function PickerSlotProvider({ children }: { children: ReactNode }) {
  const [slot, setSlot] = useState<HTMLElement | null>(null);
  return (
    <SetSlotContext.Provider value={setSlot}>
      <SlotContext.Provider value={slot}>{children}</SlotContext.Provider>
    </SetSlotContext.Provider>
  );
}

export function usePickerSlot(): HTMLElement | null {
  return useContext(SlotContext);
}

/** Ref callback for the header's slot element. */
export function useSetPickerSlot(): (element: HTMLElement | null) => void {
  return useContext(SetSlotContext);
}
