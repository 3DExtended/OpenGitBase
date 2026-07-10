import { communityPitchLinks } from '../utils/communityPitchLinks'

export interface PitchLink {
  label: string
  href: string
  primary?: boolean
}

export interface PitchSlide {
  id: string
  layout: 'title' | 'default' | 'columns' | 'cta'
  title?: string
  subtitle?: string
  lead?: string
  bullets?: string[]
  columns?: Array<{ heading: string, items: string[] }>
  note?: string
  links?: PitchLink[]
}

export const communityPitchSlides: PitchSlide[] = [
  {
    id: 'title',
    layout: 'title',
    title: 'Git that\u2019s yours to design',
    subtitle:
      'Source-open. Privacy-first. Transparent by default — your code, your infra, your rules.',
  },
  {
    id: 'vision',
    layout: 'default',
    title: 'Why OpenGitBase exists',
    bullets: [
      'A self-hosted forge where you can see what the software does with your repositories',
      'No black-box hosting — run it yourself or trust a host you choose',
      'Built for people who care where code lives and how the platform is run',
      'Community-shaped direction: contributors help decide what we build next',
    ],
  },
  {
    id: 'yours-to-design',
    layout: 'default',
    title: 'Yours to design',
    lead: 'Transparency is the product promise — not a marketing footnote.',
    bullets: [
      'Read every line: the full codebase is public and forkable',
      'Self-host the stack: API, web UI, git dispatchers, and storage nodes',
      'Audit replication, access checks, and push rules — they are in the repo',
      'Shape the roadmap: pick a lane and own a slice of the forge',
    ],
  },
  {
    id: 'privacy',
    layout: 'default',
    title: 'Privacy-first git',
    bullets: [
      'Your repositories stay on infrastructure you control',
      'Sensitive control-plane data (emails, fleet secrets) encrypted at rest in the API',
      'Git auth via your SSH keys or personal access tokens — not vendor SSO lock-in',
      'Honest scope: repository object data is not encrypted at rest yet — that is on the roadmap',
    ],
  },
  {
    id: 'license',
    layout: 'default',
    title: 'Contributing is open. Production has conditions.',
    lead: 'Source-available — not OSI-approved open source. We separate building from deploying.',
    bullets: [
      'Fork, read, modify, and contribute without a commercial license',
      'Development and testing are always unrestricted',
      'Free production for qualifying internal small teams and OSI-licensed projects',
      'Commercial license required for large orgs and third-party hosting — see LICENSE',
    ],
    links: [
      { label: 'License', href: communityPitchLinks.license },
      { label: 'Licensing FAQ', href: communityPitchLinks.licensingFaq },
    ],
  },
  {
    id: 'differentiation',
    layout: 'default',
    title: 'Replication as a first-class product',
    subtitle: 'Not bolted-on backups — distributed git storage is the architecture.',
    bullets: [
      'RF=3 by default: primary plus two replicas, 2/3 write quorum',
      'Peer sync over mTLS between storage nodes — separate from git client paths',
      'Watermark-based durability and epoch failover against split-brain writes',
      'Full forge surface: merge requests, discussions, branch protection, admin fleet UI',
    ],
  },
  {
    id: 'architecture',
    layout: 'columns',
    title: 'How it fits together',
    columns: [
      {
        heading: 'Control plane',
        items: [
          '.NET API + Postgres',
          'Auth, metadata, replication orchestration',
          'Web content reads from storage',
        ],
      },
      {
        heading: 'Git path',
        items: [
          'Dispatchers proxy HTTPS / SSH git',
          'Access checks before every operation',
          'Writes routed to primary replica',
        ],
      },
      {
        heading: 'Storage fleet',
        items: [
          'Three (or more) storage nodes',
          'Bare git + hooks + quorum replicate',
          'Background failover and anti-entropy',
        ],
      },
    ],
    note: 'Deep dive: PROJECT-STATE.md in the repository',
    links: [{ label: 'Project state doc', href: communityPitchLinks.projectState }],
  },
  {
    id: 'transparency-now',
    layout: 'default',
    title: 'Transparent today',
    bullets: [
      'Entire product source is public — issues, PRs, and design docs in the open',
      'Self-hosters can inspect replication, auth, and storage behavior directly',
      'No hidden telemetry requirement to run the platform',
      'Architecture baseline published for agents and contributors',
    ],
  },
  {
    id: 'transparency-future',
    layout: 'default',
    title: 'Transparent operations — the plan',
    lead: 'A hobby project today. We intend to publish how the project itself is run — without fake deadlines.',
    bullets: [
      'Public roadmap — priorities visible to everyone',
      'Public finance page — costs and sustainability in the open',
      'Operational reports — uptime, incidents, hosting spend',
      'Governance charter — how contributors influence decisions',
    ],
    note: 'These ship when bandwidth allows. No hard timelines yet.',
  },
  {
    id: 'where-we-are',
    layout: 'default',
    title: 'Where we are',
    bullets: [
      'Production-grade problems, nights-and-weekends pace',
      'Maintained today by @3dextended — bus factor 1 until you help change that',
      '1,000+ automated tests; HA storage and forge features are real, not vapor',
      'Looking for co-maintainers across storage, security, web, tests, and docs',
    ],
    links: [
      {
        label: '@3dextended on GitHub',
        href: communityPitchLinks.maintainerGithub,
      },
    ],
  },
  {
    id: 'contribute-lanes',
    layout: 'columns',
    title: 'Pick your lane',
    subtitle: 'Every area needs builders — no single golden path.',
    columns: [
      {
        heading: 'Distributed storage',
        items: ['Quorum replication', 'Failover & epochs', 'mTLS peer sync'],
      },
      {
        heading: 'Security & encryption',
        items: ['Fleet endpoint trust', 'Secrets hardening', 'At-rest encryption'],
      },
      {
        heading: 'Web & forge UX',
        items: ['Merge requests UI', 'Repo browsing', 'Collaboration threads'],
      },
      {
        heading: 'Tests & docs',
        items: ['E2E regression', 'Onboarding docs', 'ADRs & roadmap'],
      },
    ],
  },
  {
    id: 'cta-contribute',
    layout: 'cta',
    title: 'Start by contributing',
    lead: 'Read the baseline, open an issue, or send a PR.',
    links: [
      { label: 'GitHub Issues', href: communityPitchLinks.issues, primary: true },
      {
        label: 'Project state (start here)',
        href: communityPitchLinks.projectState,
      },
      { label: 'Contributing guide', href: communityPitchLinks.contributing },
      { label: 'Source repository', href: communityPitchLinks.github },
    ],
  },
  {
    id: 'cta-run',
    layout: 'cta',
    title: 'Or run it locally',
    lead: 'Three storage nodes, dispatchers, and the full stack in Docker Compose.',
    links: [
      {
        label: 'Setup guide (README)',
        href: communityPitchLinks.readme,
        primary: true,
      },
      { label: 'Project state doc', href: communityPitchLinks.projectState },
    ],
    note: 'Requires Docker, curl, and openssl. Fleet bootstrap takes ~15 minutes first time.',
  },
]
