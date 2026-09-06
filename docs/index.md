---
layout: home

hero:
  name: LambdaWidgets
  text: Build CloudWatch custom widgets, and develop them locally
  tagline: A C# framework for custom widget Lambdas, and a console-shaped dashboard that runs on your machine. The dashboard works with a widget in any language.
  actions:
    - theme: brand
      text: What is a custom widget
      link: /guide/what-is-a-custom-widget
    - theme: alt
      text: The console contract
      link: /reference/console-contract
    - theme: alt
      text: Status
      link: /status

features:
  - title: The framework
    details: LambdaWidgets.Runtime and LambdaWidgets.Testing. A widget host for a Hardened application, Razor helpers that emit cwdb-action from generated links, form binding, describe, and a click-through test driver.
  - title: The harness
    details: lambda-widgets, a dashboard that runs locally, interprets clicks and forms the way the console does, and invokes your widget through any Lambda Invoke API endpoint. Native binaries, a Docker image, and a dotnet tool.
  - title: The console contract, written down
    details: The event, cwdb-action, the rendering rules and the response shapes, in one reference. AWS documents these across four pages and a samples repository.
---

## The problem

A CloudWatch custom widget is a dashboard widget backed by a Lambda function. The dashboard invokes
the function, the function returns HTML, and a `cwdb-action` element in that HTML re-invokes the
function when the user clicks. One widget is a whole server-rendered application inside the console.

Developing one means deploying a Lambda, opening a dashboard, clicking, reading a log, and editing.
There is no local loop for this in any language. The official samples are Python and JavaScript
scripts, and nobody has written a framework for it.

## What this repository does about it

`lambda-widgets` runs the console's side of that contract on your machine. It builds the same event,
parses the HTML your function returns, interprets `cwdb-action` the way the console does, applies the
same sanitizer, and invokes your function through the AWS Lambda Test Tool, the runtime interface
emulator, SAM, or the real Lambda service. Your function does not know the difference.

The C# framework is the other half. A widget application is web-shaped: pages are ordinary routes,
the generated `Links` type gives them names, and the Razor helpers turn a link into the
`cwdb-action` the console expects. Rename a handler and the template that links to it fails at
build time.
