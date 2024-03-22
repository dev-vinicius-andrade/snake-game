# Introduction

This is an Angular 17 project that provides an User Interface to play a multiplayer Snake Game.

The game uses SignalR to provide real-time communication between the players and the server.

## Requirements

- Node.js ^18.17.1
- NPM ^10.5.0
- Running Server you can find it in the same repository
[Optional]
- [Docker Requirements](#docker-requirements)

### Docker Requirements

- Docker
- Docker Compose

## Configurations

All the configurations used by the application are stored in src/environments/environment.ts file.
Which allow changing it while running as docker container without the need to rebuild the image, just passing the new values as environment variables.

## Debugging

This project has a VSCode workspace configuration that allows debugging the application using the Chrome/Edge browser.
To do so, just open the workspace in VSCode and run the debug configuration named "localhost (Edge/Chrome)".

## Building

You can build the project using the following command:

```bash
  npm install
  npm run build
```

### Building as Docker Image

You can build the docker image using the following command:

```bash
  docker build -t snake-game-angular --no-cache .
```

## Running

You can run the project using the following command:

```bash
  npm install
  npm run start
```

### Running as Docker Container

PS: The docker image must be built before running it.
You can run the docker container using the following command:

```bash
  docker run -d -p 4200:80 snake-game-angular
```

### Running as Docker Compose

If you want to test the whole application there is a docker-compose file that will run the Angular application and the server applications.

You can run the docker-compose using the following command:

```bash
  docker-compose up
```

## Used Technologies

- [Angular](https://angular.io/)
- [SignalR^7.0.14](https://www.npmjs.com/package/@microsoft/signalr)
- [PrimeNG^17.11.0](https://www.primefaces.org/primeng/)
- [Angular Material^17.2.2](https://material.angular.io/)
- [Axios^1.6.7](https://www.npmjs.com/package/axios)
- [PrimeIcons^6.0.1](https://www.primefaces.org/primeicons/)
- [angular-server-side-configuration^17.0.2](https://www.npmjs.com/package/angular-server-side-configuration)
- [TailwindCSS^3.4.1](https://tailwindcss.com/)

## File Structure

You can find the following file structure in this project:

- src (source code)
  - app (application code)
    - components (all the components used in the application)
    - layouts (all the layouts used in the application)
    - services (all the services used in the application)
    - pages (all the pages used in the application)
    - app-routing.module.ts (the application routing module)
    - app.component.html (the application main component template)
    - app.component.scss (the application main component styles)
    - app.component.ts (the application main component)
    - app.module.ts (the application main module)
  - assets (all the assets used in the application)
  - environments (all the environments configurations)
  - themes (all the themes used in the application)
  - index.html (the application main html file)
  - main.ts (the application main typescript file)
  - favicon.ico (the application favicon)
  - styles.scss (the application global styles)
- angular.json (the angular configuration file)
- package.json (the npm configuration file)
- tsconfig.app.json (the typescript configuration file)
- tailwind.config.js (the tailwindcss configuration file)
