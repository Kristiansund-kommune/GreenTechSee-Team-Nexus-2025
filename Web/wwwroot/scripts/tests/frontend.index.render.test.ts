// This test verifies that the Vue SFC Index.vue renders and displays the expected heading text.
import { describe, expect, test } from "vitest";
import { mount } from "@vue/test-utils";
import Index from "../index.vue";

describe("Index.vue rendering", () => {
	test("renders heading 'Hello, Vue 3!'", () => {
		const wrapper = mount(Index);
		expect(wrapper.text()).toContain("Hello, Vue 3!");
	});
});


