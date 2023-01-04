import type { RouteRecordRaw } from "vue-router";
import { useRouteParamsAsProperties } from "@/composables/useRouteParamsAsProperties";
export default [
  {
    path: "/",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "home",
        props: useRouteParamsAsProperties,
        component: () => import("@views/Home.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
