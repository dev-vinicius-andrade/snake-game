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
      {
        path: "profile",
        name: "profile",
        component: () => import("@/views/account/profile.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
