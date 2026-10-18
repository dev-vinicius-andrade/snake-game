import type { RouteRecordRaw } from "vue-router";
export default [
  {
    path: "/join/:code",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "join",
        props: true,
        component: () => import("@/views/join/index.vue"),
      },
    ],
  },
  {
    path: "/about",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "about",
        component: () => import("@/views/about/index.vue"),
      },
    ],
  },
  {
    path: "/admin",
    component: () => import("@layouts/Blank.vue"),
    children: [
      {
        path: "",
        name: "admin",
        component: () => import("@/views/admin/index.vue"),
      },
    ],
  },
] as RouteRecordRaw[];
