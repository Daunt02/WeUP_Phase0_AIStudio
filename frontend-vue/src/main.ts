import { createApp } from "vue";
import { Quasar } from "quasar";
import "quasar/src/css/index.sass";

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
