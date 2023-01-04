import { createRouter, createWebHistory } from "vue-router";
import { useImportRoutes } from "@/composables/useImportRoutes";
const routes = useImportRoutes(
  import.meta.glob("@/router/routes/**/*.*s", { eager: true })
);
const router = createRouter({
  history: createWebHistory(process.env.BASE_URL),
  routes,
});

export default router;
