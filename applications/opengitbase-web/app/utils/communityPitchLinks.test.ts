import { describe, expect, it } from 'vitest'

import { communityPitchLinks } from './communityPitchLinks'

describe('communityPitchLinks', () => {
  it('uses GitHub for contribute paths', () => {
    expect(communityPitchLinks.github).toBe(
      'https://github.com/3DExtended/OpenGitBase',
    )
    expect(communityPitchLinks.issues).toContain('/issues')
    expect(communityPitchLinks.projectState).toContain('PROJECT-STATE.md')
    expect(communityPitchLinks.contributing).toContain('CONTRIBUTING.md')
  })

  it('uses hosted README for run locally CTA', () => {
    expect(communityPitchLinks.readme).toBe(
      'https://www.opengitbase.com/opengitbase/open-git-base',
    )
  })

  it('links maintainer handle', () => {
    expect(communityPitchLinks.maintainerGithub).toBe(
      'https://github.com/3dextended',
    )
  })
})
