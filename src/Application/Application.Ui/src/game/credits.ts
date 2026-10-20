/** What the game uses that other people made, and under which licence (shown on the credits page). */
export const CREDIT_GROUPS = ["art", "fonts", "front", "back"] as const;

export type CreditGroup = (typeof CREDIT_GROUPS)[number];

export interface Credit {
  name: string;
  /** Who made it. */
  by: string;
  license: string;
  /** Where the licence can be read, when it is not on the project's own page. */
  licenseUrl?: string;
  url: string;
  group: CreditGroup;
}

const CC_BY_3 = "https://creativecommons.org/licenses/by/3.0/";
const APACHE = "https://www.apache.org/licenses/LICENSE-2.0";
const MIT = "https://opensource.org/license/mit";
const OFL = "https://openfontlicense.org/";

export const CREDITS: readonly Credit[] = [
  { name: "game-icons.net", by: "Lorc, Delapouite and contributors", license: "CC BY 3.0", licenseUrl: CC_BY_3, url: "https://game-icons.net", group: "art" },
  { name: "Material Design Icons", by: "Pictogrammers", license: "Apache 2.0", licenseUrl: APACHE, url: "https://pictogrammers.com/library/mdi/", group: "art" },

  { name: "Press Start 2P", by: "CodeMan38", license: "SIL OFL 1.1", licenseUrl: OFL, url: "https://fonts.google.com/specimen/Press+Start+2P", group: "fonts" },
  { name: "Roboto", by: "Google", license: "Apache 2.0", licenseUrl: APACHE, url: "https://fonts.google.com/specimen/Roboto", group: "fonts" },

  { name: "Vue", by: "Evan You and contributors", license: "MIT", licenseUrl: MIT, url: "https://vuejs.org", group: "front" },
  { name: "Vuetify", by: "John Leider and contributors", license: "MIT", licenseUrl: MIT, url: "https://vuetifyjs.com", group: "front" },
  { name: "Vue Router, Pinia, Vue I18n, VueUse", by: "Vue community", license: "MIT", licenseUrl: MIT, url: "https://github.com/vuejs", group: "front" },
  { name: "MediaPipe Tasks Vision", by: "Google", license: "Apache 2.0", licenseUrl: APACHE, url: "https://developers.google.com/mediapipe", group: "front" },
  { name: "CodeMirror", by: "Marijn Haverbeke and contributors", license: "MIT", licenseUrl: MIT, url: "https://codemirror.net", group: "front" },
  { name: "SignalR client", by: "Microsoft", license: "MIT", licenseUrl: MIT, url: "https://github.com/dotnet/aspnetcore", group: "front" },
  { name: "Axios", by: "Axios contributors", license: "MIT", licenseUrl: MIT, url: "https://axios-http.com", group: "front" },
  { name: "Lodash", by: "OpenJS Foundation and contributors", license: "MIT", licenseUrl: MIT, url: "https://lodash.com", group: "front" },

  { name: ".NET and ASP.NET Core", by: "Microsoft and the .NET Foundation", license: "MIT", licenseUrl: MIT, url: "https://dotnet.microsoft.com", group: "back" },
  { name: "Entity Framework Core", by: "Microsoft", license: "MIT", licenseUrl: MIT, url: "https://github.com/dotnet/efcore", group: "back" },
  { name: "Npgsql", by: "Npgsql contributors", license: "PostgreSQL License", url: "https://www.npgsql.org", group: "back" },
  { name: "RabbitMQ .NET client", by: "Broadcom and contributors", license: "Apache 2.0 / MPL 2.0", url: "https://github.com/rabbitmq/rabbitmq-dotnet-client", group: "back" },
  { name: "Jint", by: "Sebastien Ros and contributors", license: "BSD 2-Clause", url: "https://github.com/sebastienros/jint", group: "back" },
  { name: "Serilog", by: "Serilog contributors", license: "Apache 2.0", licenseUrl: APACHE, url: "https://serilog.net", group: "back" },
  { name: "StackExchange.Redis", by: "Stack Exchange and contributors", license: "MIT", licenseUrl: MIT, url: "https://github.com/StackExchange/StackExchange.Redis", group: "back" },
  { name: "Swashbuckle", by: "Swashbuckle contributors", license: "MIT", licenseUrl: MIT, url: "https://github.com/domaindrivendev/Swashbuckle.AspNetCore", group: "back" },
];
