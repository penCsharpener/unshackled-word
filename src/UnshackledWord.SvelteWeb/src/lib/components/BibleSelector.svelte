<script lang="ts">
let books = ["Genesis", "Exodus", "Leviticus"]; // Mock data

// Svelte 5 Runes for reactive state
let selectedBook = $state(books[0]);
let selectedChapter = $state(1);

// $derived updates automatically when its dependencies change (like a computed property)
let chapters = $derived(selectedBook === "Genesis" ? 50 : 40);
</script>

<div class="flex gap-4 p-4 border rounded bg-white">
	<select bind:value={selectedBook} class="p-2 border rounded">
		{#each books as book}
			<option value={book}>{book}</option>
		{each}
	</select>

	<select bind:value={selectedChapter} class="p-2 border rounded">
		{#each Array.from({length: chapters}, (_, i) => i + 1) as cap}
			<option value={cap}>Chapter {cap}</option>
		{each}
	</select>
</div>