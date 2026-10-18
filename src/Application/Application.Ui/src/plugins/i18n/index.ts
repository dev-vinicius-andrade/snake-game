import { createI18n } from 'vue-i18n';
import { DEFAULT_LOCALE } from '@/locales/locale';
import en from '@/locales/messages/en';
import pt from '@/locales/messages/pt';

const i18n = createI18n({
  legacy: false,
  locale: DEFAULT_LOCALE,
  fallbackLocale: DEFAULT_LOCALE,
  messages: { en, pt },
  fallbackWarn: process.env.NODE_ENV === 'production'? false:true,
  missingWarn:  process.env.NODE_ENV === 'production'? false:true,
});

export default i18n
