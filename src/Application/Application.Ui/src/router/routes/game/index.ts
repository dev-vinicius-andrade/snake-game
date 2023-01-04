import type { RouteRecordRaw } from "vue-router";
import { useRouteParamsAsProperties } from "@/composables/useRouteParamsAsProperties";
export default [
  {
    path: "/game",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "/snake",
        component: () => import("@layouts/Blank.vue"),
        children: [
          {
            path: "2d",
            name: "/game/snake/2d",
            props: useRouteParamsAsProperties,
            component: () => import("@/views/snake/2d/index.vue"),
          },
        ],
      },
    ],
  },
] as RouteRecordRaw[];
