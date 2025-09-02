import { fileURLToPath } from "node:url";
import { mergeConfig, defineConfig, configDefaults } from "vitest/config";
import viteConfig from "./vite.config";

export default mergeConfig(
	viteConfig,
	defineConfig({
		plugins: [
			<any>{
				enforce: "pre",
				transform(code: any, id: string) {
					if (/\.(css|sass|scss)$/.test(id)) {
						// ignorere kode-importerte stylesheets i testene
						return { code: "" };
					}
				}
			}
		],
		resolve: {
			alias: {
			}
		},
		test: {
			globals: true,
			environment: "jsdom",
			exclude: [
				...configDefaults.exclude,
				"e2e/*",
				"**/obj/**"
			],
			root: fileURLToPath(new URL("./", import.meta.url))
		}
	})
);
