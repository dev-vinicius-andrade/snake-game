# The Vue site as static files behind nginx. Build context: src/Application/Application.Ui
#   podman build --build-arg VITE_MANAGER_URL=/api -t <image> src/Application/Application.Ui
# /api is the manager behind this same host (see nginx.conf); an absolute url works too.
FROM docker.io/library/node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
# The manager url is read at build time by Vite.
ARG VITE_MANAGER_URL
ENV VITE_MANAGER_URL=$VITE_MANAGER_URL
RUN npm run build

# nginx-unprivileged: runs as user 101 and listens on 8080, so it fits a non-root, read-only pod.
FROM docker.io/nginxinc/nginx-unprivileged:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY proxy_common.inc /etc/nginx/conf.d/proxy_common.inc
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
