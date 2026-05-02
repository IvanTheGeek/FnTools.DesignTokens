---
id: 003
title: All file I/O is provided by the caller via a load function
status: accepted
date: 2026-05-02
---

## Context

The resolver needs to load token set files referenced by a `.resolver.json`. The naive implementation would call `File.ReadAllText` directly. This works on a desktop but fails in Figma plugins (sandboxed JS runtime), WASM targets (no filesystem access), CI pipelines (virtual filesystems), and unit tests (in-memory fixtures).

## Decision

The resolver's public API takes a `loadFile: string -> Result<string, string>` parameter. The library never calls any I/O function directly. The caller provides the implementation appropriate for its host environment.

## Consequences

- Unit tests pass `Map.tryFind`-based in-memory loaders with zero real filesystem access.
- WASM hosts can provide a fetch-based loader.
- CLI tools provide `File.ReadAllText`-based loaders.
- The library has no dependency on `System.IO` beyond what BCL types already pull in.
- This pattern must be maintained for any future layer that needs external resources (e.g., a future schema validator that fetches `$schema` URLs).
