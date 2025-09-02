import { fileURLToPath, URL } from "node:url";

import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import fs from "fs";

import packageJson from "./package.json";
const dependencies = [];
for (const dep of Object.keys(packageJson.dependencies)) {
	dependencies.push(dep);
}

// https://vitejs.dev/config/
export default defineConfig({
	base: "",
	root: "Web/wwwroot",
	plugins: [
		vue()
	],
	resolve: {
		alias: {
			"@": fileURLToPath(new URL("./Web/wwwroot", import.meta.url))
		}
	},
	server : {
		port: 5174,
		https : {
			key: fs.readFileSync("./cert/localhost.key"),
			cert: fs.readFileSync("./cert/localhost.crt"),
		},
		cors: {
			origin: "https://localhost:7039"
		}
	},
	optimizeDeps: {
		include: dependencies,
	},
	css: {
		preprocessorOptions: {
			scss: {
				quietDeps: true,
				silenceDeprecations: ["global-builtin", "import"]
			}
		}
	},
	build: {
		manifest: true,
		cssCodeSplit: false,
		target: "modules",
		rollupOptions: {
			input: [
				"scripts/index.vue",
			].map(str => "Web/wwwroot/" + str),
			output: {
				entryFileNames: `assets/[name].js`,
				chunkFileNames: `assets/chunk-[hash].js`,
				assetFileNames: assetInfo => {
					if (assetInfo.name === "style.css") {
						return "assets/style.css";
					}
					return `assets/[name]-[hash].[ext]`;
				}
			}
		},
		assetsInlineLimit: (file) => {
			const ext = file.toLowerCase().split('.').pop();
			return ext !== "svg"
				&& ext !== "woff"
				&& ext !== "woff2"
				&& ext !== "ttf";
		}
	}
});
