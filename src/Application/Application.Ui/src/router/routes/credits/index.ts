import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/credits",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "credits",
        component: () => import("@/views/credits/index.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
