// This test ensures the application entry (main.ts) mounts the Vue app into the #entrypoint element.
import { describe, expect, test } from "vitest";

describe("main.ts mounting", () => {
	test("mounts app into #entrypoint and renders content", async () => {
		const container = document.createElement("div");
		container.id = "entrypoint";
		document.body.appendChild(container);
		await import("../main");
		expect(container.innerHTML).toContain("Hello, Vue 3!");
		document.body.removeChild(container);
	});
});


