import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import ids from './achievementIds.json'
import { ACHIEVEMENT_TEXTS } from './achievementTexts'

const csPath = fileURLToPath(
  new URL('../../../../Library/Library.Commons.Game.Domain/Entities/Achievements.cs', import.meta.url),
)
const csSource = readFileSync(csPath, 'utf8')

const DEFAULTS_RE = /new\("([^"]+)",\s*"[^"]*",\s*StatKeys\.(\w+),\s*(\d+)\)/g
const defaults = [...csSource.matchAll(DEFAULTS_RE)].map((m) => ({
  id: m[1],
  stat: m[2],
  threshold: Number(m[3]),
}))

const LANGS = ['en', 'pt'] as const

describe('achievement texts', () => {
  it('parses the 17 default achievements from Achievements.cs', () => {
    expect(defaults.length).toBe(17)
  })

  it('ids in the JSON match the C# defaults in order', () => {
    expect(ids).toEqual(defaults.map((d) => d.id))
  })

  for (const lang of LANGS) {
    describe(lang, () => {
      it('has exactly the ids of the JSON', () => {
        expect(Object.keys(ACHIEVEMENT_TEXTS[lang]).sort()).toEqual([...ids].sort())
      })

      it('every name and description is non-empty and within length limits', () => {
        for (const id of ids) {
          const text = ACHIEVEMENT_TEXTS[lang][id]
          expect(text, `${lang}/${id}`).toBeDefined()
          expect(text.name.trim().length, `${lang}/${id} name`).toBeGreaterThan(0)
          expect(text.description.trim().length, `${lang}/${id} description`).toBeGreaterThan(0)
          expect(text.name.length, `${lang}/${id} name length`).toBeLessThan(28)
          expect(text.description.length, `${lang}/${id} description length`).toBeLessThan(90)
        }
      })

      it('thresholds in descriptions match Achievements.cs', () => {
        for (const d of defaults) {
          const description = ACHIEVEMENT_TEXTS[lang][d.id].description
          if (d.stat === 'PlaySeconds') {
            expect(/\d/.test(description) && /hour|hora/i.test(description), `${lang}/${d.id}`).toBe(true)
            continue
          }
          expect(description, `${lang}/${d.id}`).toMatch(new RegExp(`\\b${d.threshold}\\b`))
        }
      })
    })
  }
})
