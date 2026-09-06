# Samples

Three widgets, built on the framework and run in the harness. They are the integration tests of both
products and the demo a reader runs with no AWS account.

| Sample | Proves | Item |
|---|---|---|
| `Echo` | The AWS Echo widget in C#. The first thing the harness renders, and the fixture for the test driver | 2 |
| `LogsSearch` | Forms, the dashboard time range, the AWS SDK under AOT, a slow call inside one invocation | 5 |
| `DynamoLookup` | Paging state carried in an action's fields, a second SDK client under AOT | 6 |
| `deploy` | A C# CDK app on `Hardened.Amz.Cdk` that puts `LogsSearch` in a real dashboard | 7 |

Each ships its dashboard JSON, so the harness opens with all three on one dashboard.

None of them exists yet. See the status table in `docs/status.md`.
