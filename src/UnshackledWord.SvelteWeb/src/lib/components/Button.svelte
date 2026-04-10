<script lang="ts">
import type { Snippet } from "svelte";

interface Props {
  onclick?: () => void;
  variant?: "primary" | "secondary";
  children: Snippet; // Snippets replace slots for passing HTML content
}

let { onclick, variant = "primary", children }: Props = $props();

const baseStyles = "px-4 py-2 rounded font-medium transition-colors";
const variants = {
  primary: "bg-blue-600 text-white hover:bg-blue-700",
  secondary: "bg-gray-200 text-gray-800 hover:bg-gray-300",
};
</script>

<button {onclick} class="{baseStyles} {variants[variant]}">
	{@render children()}
</button>