import { describe, expect, test } from 'bun:test'
import { SiteNode } from './site'

describe('SiteNode', () => {
  test('renders ground by default for existing scenes', () => {
    expect(SiteNode.parse({}).renderGround).toBe(true)
  })

  test('accepts scenes that hide only the site ground visuals', () => {
    expect(SiteNode.parse({ renderGround: false }).renderGround).toBe(false)
  })

  test('allows hidden ground to remain a shadow catcher', () => {
    const site = SiteNode.parse({ renderGround: false, shadowCatcher: true })

    expect(site.shadowCatcher).toBe(true)
  })
})
