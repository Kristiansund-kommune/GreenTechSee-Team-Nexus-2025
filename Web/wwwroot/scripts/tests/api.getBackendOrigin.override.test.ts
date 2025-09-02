// This test verifies that getBackendOrigin() uses a global override when provided.
import { describe, expect, test } from "vitest";
import { getBackendOrigin } from "../api";

describe("getBackendOrigin override", () => {
	test("returns custom origin if global __BACKEND_ORIGIN__ is set", () => {
		(globalThis as any).__BACKEND_ORIGIN__ = "https://api.example.test";
		const origin = getBackendOrigin();
		expect(origin).toBe("https://api.example.test");
		delete (globalThis as any).__BACKEND_ORIGIN__;
	});
});


