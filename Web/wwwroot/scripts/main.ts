import "./core";
import { createApp } from "vue";
import Index from "./index.vue";

console.log("main.ts");

const app = createApp(Index);

app.mount("#entrypoint");
