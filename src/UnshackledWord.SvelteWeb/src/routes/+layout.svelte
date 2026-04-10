<script lang="ts">
import "../app.css"; // Tailwind import
let { children } = $props(); // 'children' is a Snippet for the page content

// Mocking auth state for now
let isLoggedIn = $state(false);

function toggleAuth() {
  isLoggedIn = !isLoggedIn;
}
</script>

<nav class="flex items-center justify-between p-4 bg-gray-800 text-white">
	<div class="text-xl font-bold">BibleApp</div>
	<div class="flex gap-4">
		<a href="/" class="hover:underline">Home</a>
		<button 
			onclick={toggleAuth}
			class="px-4 py-2 bg-blue-600 rounded hover:bg-blue-700 transition"
		>
			{isLoggedIn ? 'Sign Out' : 'Sign In'}
		</button>
	</div>
</nav>

<main class="p-6">
	{@render children()}
</main>