import { RouteLocationNormalized } from "vue-router";

export function useRouteParamsAsProperties(route: RouteLocationNormalized) {
  const { params, ...routeWithoutParams } = route;
  return {
    ...params,
    routeWithoutParams,
  };
}
export default useRouteParamsAsProperties;
