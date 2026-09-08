# Invoke targets

::: warning Partly shipped
The proxy, `--test-tool`, `--rie`, `--sam` and `--function` are built. `--aws` is not: it needs the
SDK path rather than the Invoke API over HTTP. See [status](/status).
:::

The function name in a `cwdb-action` endpoint, the segment after `function:`, selects where the
harness sends the invoke.

| Flag | Address | Path | Notes |
|---|---|---|---|
| `--test-tool [port]` | `http://localhost:5050` | `/2015-03-31/functions/{name}/invocations` | The default. Every name maps here. |
| `--rie <url>` | `http://localhost:9000` | `/2015-03-31/functions/function/invocations` | One function per container, so the name is ignored. Port unverified. |
| `--sam <url>` | `http://127.0.0.1:3001` | `/2015-03-31/functions/{name}/invocations` | `sam local start-lambda`. Unverified. |
| `--aws [--region r]` | the Lambda service | SDK `Invoke` | The full ARN including any alias, through the default credential chain. |
| `--function name=url` | any | `/2015-03-31/functions/{name}/invocations` | Explicit, repeatable. Wins over the defaults. |

`--aws` is the one worth knowing about. It develops the page against real data and a real execution
role without touching a dashboard, which is the case the console makes most expensive.

## The proxy

Every call goes server side. The page never calls a target directly, for two reasons: the test tool's
Invoke API sets no CORS headers, and credentials for `--aws` must not reach the browser.

The Invoke request is a JSON body. The response body is the function's result, which is a JSON string
for HTML or an object otherwise. A response carrying the `X-Amz-Function-Error` header is rendered as
an error block with the payload in it.

The proxy's timeout is 60 seconds, because a Logs Insights query is polled inside the function and a
single invoke can legitimately take that long.
