// This test ensures that fetchJson() throws a descriptive error when response.ok is false.
import { describe, expect, test, vi } from "vitest";
import { fetchJson } from "../api";

describe("fetchJson error handling", () => {
	test("throws on non-200 response with status in message", async () => {
		const mockResponse = new Response("bad", { status: 500, statusText: "Server Error" });
		vi.spyOn(globalThis, "fetch" as any).mockResolvedValue(mockResponse as any);
		await expect(fetchJson("/health")).rejects.toThrow(/HTTP 500/);
	});
});


