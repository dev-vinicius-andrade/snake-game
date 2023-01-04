import { getCurrentInstance } from "vue";
import { useDisplay } from "vuetify";
export function useVuetifyDisplay(): any {
  const instance = getCurrentInstance();
  if (!instance) {
    throw new Error(`useVuetify should be called in setup().`);
  }
  return useDisplay();
}
