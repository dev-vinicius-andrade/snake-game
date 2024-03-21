/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors:{
        app:{
          background:"rgb(var(--color-app-background) / <alpha-value>)",
          text:'rgb(var(--color-app-text) / <alpha-value>)',
        },
      },
       boxShadow: {
        sm: '0 1px 2px 0 rgb(var(--boxshadow-app-color) / 0.05)',
        DEFAULT: '0 1px 3px 0 rgb(var(--boxshadow-app-color) / 0.1), 0 1px 2px -1px rgb(var(--boxshadow-app-color) / 0.1)',
        md: '0 4px 6px -1px rgb(var(--boxshadow-app-color) / 0.1), 0 2px 4px -2px rgb(var(--boxshadow-app-color) / 0.1)',
        lg: '0 10px 15px -3px rgb(var(--boxshadow-app-color) / 0.1), 0 4px 6px -4px rgb(var(--boxshadow-app-color) / 0.1)',
        xl: '0 20px 25px -5px rgb(var(--boxshadow-app-color) / 0.1), 0 8px 10px -6px rgb(var(--boxshadow-app-color) / 0.1)',
        '2xl': '0 25px 50px -12px rgb(var(--boxshadow-app-color) / 0.25)',
        inner: 'inset 0 2px 4px 0 rgb(var(--boxshadow-app-color) / 0.05)',
        none: 'none',
       }
    },
  },
  plugins: [],
}

