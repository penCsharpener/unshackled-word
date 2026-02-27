import {
  Combobox,
  ComboboxButton,
  ComboboxInput,
  ComboboxOption,
  ComboboxOptions,
} from "@headlessui/react";
import { CheckIcon, ChevronUpDownIcon } from "@heroicons/react/20/solid"; // Optional icons
import clsx from "clsx";
import { useState } from "react";
import { PartOfSpeech } from "../../types";

// Format enum into a searchable list
const posList = Object.keys(PartOfSpeech)
  .filter((key) => Number.isNaN(Number(key)))
  .map((key) => ({
    id: PartOfSpeech[key as keyof typeof PartOfSpeech] as number,
    name: key,
  }));

interface Props {
  onChange: (id: number) => void;
}

export default function PartOfSpeechSearch({ onChange }: Props) {
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState(posList[0]);

  const filteredPos =
    query === ""
      ? posList
      : posList.filter((pos) =>
          pos.name.toLowerCase().includes(query.toLowerCase()),
        );

  const handleSelect = (pos: (typeof posList)[0]) => {
    setSelected(pos);
    onChange(pos.id); // Returns the numeric enum value to parent
  };

  return (
    <div className="w-72">
      <Combobox value={selected} onChange={handleSelect}>
        <div className="relative mt-1">
          <div className="relative w-full cursor-default overflow-hidden rounded-lg bg-white text-left shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-white/75 focus-visible:ring-offset-2 focus-visible:ring-offset-teal-300 sm:text-sm">
            <ComboboxInput
              className="w-full border-none py-2 pl-3 pr-10 text-sm leading-5 text-gray-900 focus:ring-0"
              displayValue={(pos: any) => pos?.name}
              onChange={(event) => setQuery(event.target.value)}
            />
            <ComboboxButton className="absolute inset-y-0 right-0 flex items-center pr-2">
              <ChevronUpDownIcon
                className="h-5 w-5 text-gray-400"
                aria-hidden="true"
              />
            </ComboboxButton>
          </div>
          <ComboboxOptions className="absolute mt-1 max-height-60 w-full overflow-auto rounded-md bg-white py-1 text-base shadow-lg ring-1 ring-black/5 focus:outline-none sm:text-sm z-50">
            {filteredPos.length === 0 && query !== "" ? (
              <div className="relative cursor-default select-none py-2 px-4 text-gray-700">
                Nothing found.
              </div>
            ) : (
              filteredPos.map((pos) => (
                <ComboboxOption
                  key={pos.id}
                  value={pos}
                  className={({ focus }) =>
                    clsx(
                      "relative cursor-default select-none py-2 pl-10 pr-4",
                      focus ? "bg-teal-600 text-white" : "text-gray-900",
                    )
                  }
                >
                  {({ selected, focus }) => (
                    <>
                      <span
                        className={clsx(
                          "block truncate",
                          selected ? "font-medium" : "font-normal",
                        )}
                      >
                        {pos.name}
                      </span>
                      {selected ? (
                        <span
                          className={clsx(
                            "absolute inset-y-0 left-0 flex items-center pl-3",
                            focus ? "text-white" : "text-teal-600",
                          )}
                        >
                          <CheckIcon className="h-5 w-5" aria-hidden="true" />
                        </span>
                      ) : null}
                    </>
                  )}
                </ComboboxOption>
              ))
            )}
          </ComboboxOptions>
        </div>
      </Combobox>
    </div>
  );
}
