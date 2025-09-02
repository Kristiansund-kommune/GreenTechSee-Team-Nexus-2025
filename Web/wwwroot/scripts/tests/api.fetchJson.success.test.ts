// This test checks that fetchJson() returns parsed JSON when the response is 200 OK.
import { describe, expect, test, vi } from "vitest";
import { fetchJson } from "../api";

describe("fetchJson success", () => {
	test("parses JSON on 200 OK", async () => {
		const mockData = { ok: true };
		const mockResponse = new Response(JSON.stringify(mockData), {
			status: 200,
			headers: { "Content-Type": "application/json" }
		});
		const spy = vi.spyOn(globalThis, "fetch" as any).mockResolvedValue(mockResponse as any);
		const data = await fetchJson("/health");
		expect(data).toEqual(mockData);
		spy.mockRestore();
	});
});


