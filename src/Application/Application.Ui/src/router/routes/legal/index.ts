import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/terms",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "terms",
        component: () => import("@/views/legal/index.vue"),
        props: { page: "terms" },
      },
    ],
  },
  {
    path: "/privacy",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "privacy",
        component: () => import("@/views/legal/index.vue"),
        props: { page: "privacy" },
      },
    ],
  },
] as RouteRecordRaw[];
