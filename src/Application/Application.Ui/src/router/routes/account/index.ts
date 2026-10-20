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
        path: "oauth",
        name: "account-oauth",
        component: () => import("@/views/account/oauth.vue"),
      },
      {
        path: "profile",
        name: "profile",
        component: () => import("@/views/account/profile.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
