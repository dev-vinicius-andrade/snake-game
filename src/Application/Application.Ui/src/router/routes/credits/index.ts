import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "credits",
        name: "credits",
        component: () => import("@/views/credits/index.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
