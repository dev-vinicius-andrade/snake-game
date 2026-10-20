import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "terms",
        name: "terms",
        component: () => import("@/views/legal/index.vue"),
        props: { page: "terms" },
      },
      {
        path: "privacy",
        name: "privacy",
        component: () => import("@/views/legal/index.vue"),
        props: { page: "privacy" },
      },
    ],
  },
] as RouteRecordRaw[];
