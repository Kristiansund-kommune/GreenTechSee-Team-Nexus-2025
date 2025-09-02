// This test validates that getBackendOrigin() falls back to the default backend URL when no override is set.
import { describe, expect, test } from "vitest";
import { getBackendOrigin } from "../api";

describe("getBackendOrigin default", () => {
	test("returns default https://localhost:7039 when not overridden", () => {
		const origin = getBackendOrigin();
		expect(origin).toBe("https://localhost:7039");
	});
});


