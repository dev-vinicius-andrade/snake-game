module.exports = {
  root: true,
  env: {
    node: true,
    browser: true,
    es2021: true,
  },
  extends: [
    'eslint:recommended',
    'plugin:vue/vue3-essential',

    '@vue/eslint-config-typescript',
    './.eslintrc-auto-import.json',
    '@antfu'
  ],
  rules: {
    'vue/multi-word-component-names': 'off',
  },
}
