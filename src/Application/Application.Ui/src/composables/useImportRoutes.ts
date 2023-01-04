import { RouteRecordRaw } from "vue-router";
export function useImportRoutes(
  globRoutes?: Record<string, { default?: any }>
): RouteRecordRaw[] {
  if (!globRoutes) return [] as RouteRecordRaw[];
  const routes: RouteRecordRaw[] = [];
  Object.entries(globRoutes).forEach(([key, route]) =>
    routes.push(...route?.default)
  );
  return routes;
}
export default useImportRoutes;
