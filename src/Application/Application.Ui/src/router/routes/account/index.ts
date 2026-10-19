import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/account",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "account",
        component: () => import("@/views/account/index.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
