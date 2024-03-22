FROM node:18.17.1 as build
WORKDIR /code
COPY package.json package-lock.json ./
RUN npm install
COPY . .
RUN npm run build-docker

FROM nginx:alpine
EXPOSE 80
# Install ngssc binary
ADD https://github.com/kyubisation/angular-server-side-configuration/releases/download/v17.0.2/ngssc_64bit /usr/sbin/ngssc
RUN chmod +x /usr/sbin/ngssc

# Add ngssc init script
COPY ./deploy/ngssc.sh /docker-entrypoint.d/ngssc.sh
RUN chmod +x /docker-entrypoint.d/ngssc.sh
COPY --from=build /code/dist/snake-game-angular/browser /usr/share/nginx/html
CMD ["nginx", "-g", "daemon off;"]
