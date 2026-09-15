# Contributing

Thanks for taking the time. This project is small and the bar is simple: it
should keep working for the people already using it.

## Getting set up

```bash
git clone https://github.com/JOSETRA44/iadetector-mcp
cd IAdetector-mcp
dotnet build
dotnet test
```

## The contract

This CLI implements [`ARSENAL-SPEC.md`](../ARSENAL-SPEC.md), shared with the other
research servers in this workspace. If you add or change a command, it must keep:

- all five output formats working, including on an **empty** result set;
- the JSON envelope shape, with `count == len(rows)`;
- the documented exit codes — an empty result is exit 0, not an error;
- `describe --json` listing your new command, with `row_fields` and an example;
- data on stdout, notes and progress on stderr.

Check it with:

```bash
arsenal doctor iadetector --live
```

## Tests

- `dotnet build` and `dotnet test` must both be green.

Tests must be **hermetic**: no network calls, no reliance on the developer's real
cache directory or credentials. Mock the transport and point the cache at a
temporary directory.

Name a test so its failure reads as a sentence about the behaviour that broke,
and add a comment when the *reason* the test exists is not obvious.

## Pull requests

- One logical change per PR.
- Update `CHANGELOG.md` under `## [Unreleased]`.
- If you change a tool's description, remember the model reads it on every call:
  say what the tool returns, when to prefer it over a sibling, and what its
  parameters mean, with a real example value.
