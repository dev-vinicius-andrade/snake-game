export const DEFAULT_MANAGER_URL = "http://localhost:60510";

export interface ManagerEnv {
  VITE_MANAGER_URL?: string;
}

/** Base url of the manager api: `VITE_MANAGER_URL` without trailing slashes, or the local docker default. */
export function resolveManagerUrl(env: ManagerEnv = import.meta.env): string {
  const configured = env.VITE_MANAGER_URL?.trim();
  return configured ? configured.replace(/\/+$/, "") : DEFAULT_MANAGER_URL;
}
