import {
  Listbox,
  ListboxButton,
  ListboxOption,
  ListboxOptions,
} from "@headlessui/react";
import { CheckIcon, ChevronUpDownIcon } from "@heroicons/react/20/solid";
import clsx from "clsx";
import { useState } from "react";
import { PartOfSpeech } from "../../types";

// Convert Enum to an array for the dropdown
const posOptions = Object.keys(PartOfSpeech)
  .filter((key) => Number.isNaN(Number(key)))
  .map((key) => ({
    id: PartOfSpeech[key as keyof typeof PartOfSpeech] as number,
    name: key,
  }));

interface Props {
  onChange: (id: number) => void;
  defaultValue?: number;
}

export default function PartOfSpeechDropdown({
  onChange,
  defaultValue = 0,
}: Props) {
  // Find initial object based on defaultValue
  const initial =
    posOptions.find((o) => o.id === defaultValue) || posOptions[0];
  const [selected, setSelected] = useState(initial);

  const handleUpdate = (value: typeof initial) => {
    setSelected(value);
    onChange(value.id); // Returns the number to the parent
  };

  return (
    <div className="w-72">
      <Listbox value={selected} onChange={handleUpdate}>
        <div className="relative mt-1">
          <ListboxButton className="relative w-full cursor-default rounded-lg bg-white py-2 pl-3 pr-10 text-left shadow-md focus:outline-none focus-visible:border-indigo-500 focus-visible:ring-2 focus-visible:ring-white/75 focus-visible:ring-offset-2 focus-visible:ring-offset-orange-300 sm:text-sm border border-gray-200">
            <span className="block truncate">{selected.name}</span>
            <span className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
              <ChevronUpDownIcon
                className="h-5 w-5 text-gray-400"
                aria-hidden="true"
              />
            </span>
          </ListboxButton>

          {/* anchor="bottom" ensures it clears the button correctly */}
          <ListboxOptions
            anchor="bottom start"
            className="w-[var(--button-width)] mt-1 max-h-60 overflow-auto rounded-md bg-white py-1 text-base shadow-lg ring-1 ring-black/5 focus:outline-none sm:text-sm z-50"
          >
            {posOptions.map((pos) => (
              <ListboxOption
                key={pos.id}
                value={pos}
                className={({ focus }) =>
                  clsx(
                    "relative cursor-default select-none py-2 pl-10 pr-4",
                    focus ? "bg-indigo-100 text-indigo-900" : "text-gray-900",
                  )
                }
              >
                {({ selected }) => (
                  <>
                    <span
                      className={clsx(
                        "block truncate",
                        selected ? "font-medium" : "font-normal",
                      )}
                    >
                      {pos.name}
                    </span>
                    {selected && (
                      <span className="absolute inset-y-0 left-0 flex items-center pl-3 text-indigo-600">
                        <CheckIcon className="h-5 w-5" aria-hidden="true" />
                      </span>
                    )}
                  </>
                )}
              </ListboxOption>
            ))}
          </ListboxOptions>
        </div>
      </Listbox>
    </div>
  );
}
