# Agent Memory and Context

## Purpose

Memory allows multiple agents inside the same
workflow to share information.

Example:

Problem Agent stores:

Issue:
Battery Problem

Location:
Colombo

Provider Agent reads:

Issue:
Battery Problem

Location:
Colombo

## Storage

Memory is linked with Workflow ID.

```text
Workflow

 |

AgentMemory

 |

Key / Value pairs
```
