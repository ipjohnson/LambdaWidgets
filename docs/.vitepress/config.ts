import { defineConfig } from 'vitepress'

// The site is served from https://ipjohnson.github.io/LambdaWidgets/, so base has to carry the
// repository name. A leading and trailing slash are both required; without them every asset URL
// on a built page resolves against the org root and 404s.
export default defineConfig({
  title: 'LambdaWidgets',
  description:
    'A C# framework for CloudWatch custom widget Lambdas, and a language-agnostic local dashboard for developing them.',
  base: '/LambdaWidgets/',
  lastUpdated: true,
  cleanUrls: true,

  head: [['link', { rel: 'icon', href: '/LambdaWidgets/favicon.svg' }]],

  themeConfig: {
    nav: [
      { text: 'Guide', link: '/guide/what-is-a-custom-widget', activeMatch: '/guide/' },
      { text: 'Reference', link: '/reference/console-contract', activeMatch: '/reference/' },
      { text: 'Status', link: '/status' }
    ],

    sidebar: {
      '/guide/': [
        {
          text: 'Understanding',
          items: [
            { text: 'What is a custom widget', link: '/guide/what-is-a-custom-widget' },
            { text: 'Why a harness', link: '/guide/why-a-harness' }
          ]
        },
        {
          text: 'Using',
          items: [
            { text: 'Running the harness', link: '/guide/running-the-harness' },
            { text: 'Starting from a template', link: '/guide/templates' },
            { text: 'Writing a widget in C#', link: '/guide/writing-a-widget' },
            { text: 'Charting data', link: '/guide/charting-data' },
            { text: 'Testing a widget', link: '/guide/testing-a-widget' },
            { text: 'Deploying', link: '/guide/deploying' }
          ]
        }
      ],
      '/reference/': [
        {
          text: 'The console',
          items: [
            { text: 'The console contract', link: '/reference/console-contract' },
            { text: 'The event', link: '/reference/event' },
            { text: 'cwdb-action', link: '/reference/cwdb-action' },
            { text: 'Rendering rules', link: '/reference/rendering-rules' }
          ]
        },
        {
          text: 'The harness',
          items: [
            { text: 'Invoke targets', link: '/reference/invoke-targets' },
            { text: 'HTTP API', link: '/reference/http-api' },
            { text: 'Linter findings', link: '/reference/linter' }
          ]
        }
      ]
    },

    socialLinks: [{ icon: 'github', link: 'https://github.com/ipjohnson/LambdaWidgets' }],

    editLink: {
      pattern: 'https://github.com/ipjohnson/LambdaWidgets/edit/main/docs/:path',
      text: 'Edit this page on GitHub'
    },

    search: { provider: 'local' },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 Ian Johnson'
    }
  }
})
