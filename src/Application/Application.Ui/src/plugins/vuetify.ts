// Styles
import "@mdi/font/css/materialdesignicons.css";
import "vuetify/styles";

// Vuetify
import { createVuetify, type ThemeDefinition } from "vuetify";

/** One dark, arcade-like palette for the whole site: green snake, amber food, violet accents. */
const snake: ThemeDefinition = {
  dark: true,
  colors: {
    background: "#0b0d17",
    surface: "#161a2b",
    "surface-variant": "#1f2540",
    "on-surface": "#e6e9f5",
    "on-background": "#e6e9f5",
    primary: "#4ade80",
    "on-primary": "#052e16",
    secondary: "#a78bfa",
    "on-secondary": "#1e1b4b",
    accent: "#fbbf24",
    info: "#60a5fa",
    success: "#4ade80",
    warning: "#fbbf24",
    error: "#f87171",
  },
};

export default createVuetify({
  theme: { defaultTheme: "snake", themes: { snake } },
  // Square corners and flat, thick-bordered surfaces: the pixel look, set once for every component.
  defaults: {
    global: { rounded: 0 },
    VBtn: { variant: "flat", color: "primary", rounded: 0 },
    VCard: { rounded: 0, elevation: 0 },
    VTextField: { variant: "outlined", density: "comfortable", color: "primary", rounded: 0, hideDetails: "auto" },
    VSelect: { variant: "outlined", density: "comfortable", color: "primary", rounded: 0, hideDetails: "auto" },
    VTable: { density: "comfortable" },
  },
});
