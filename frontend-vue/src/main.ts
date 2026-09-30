import { createApp } from "vue";
import { Quasar } from "quasar";
import "quasar/src/css/index.sass";
/* WEUP 2.5D (D04): canonical depth/elevation/motion tokens. Quasar brand below untouched. */
import "./styles/weup-2.5d-tokens.css";

import App from "./App.vue";

const app = createApp(App);
app.use(Quasar, {
  config: {
    brand: {
      primary: "#2563EB",
      secondary: "#0B6E4F",
      accent: "#C44536",
    },
  },
});

app.mount("#app");
