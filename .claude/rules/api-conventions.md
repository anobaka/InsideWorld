---
paths:
  - "**/Bakabase.Service/**"
  - "**/*Controller.cs"
  - "**/*InputModel.cs"
  - "**/*ViewModel.cs"
  - "**/*ResponseModel.cs"
  - "**/Constants/**"
---

# API Conventions

See the root `CLAUDE.md` **SDK Generation** section for how `yarn gen-sdk`
works under the hood. The rules below cover **when** you need it.

## Regenerate the SDK after any of these

- Add/rename a controller action, or change its route/signature/response
- Add/modify a request/response model (`*InputModel` / `*ViewModel` /
  `*ResponseModel` / DTO)
- Change a `[SwaggerOperation]` attribute, swagger filter, or DI binding
  that shapes the OpenAPI schema
- Add/rename a `public enum` consumed by the frontend
- Change any static server-side data exposed via `constants.ts`
  (see "Static data for the frontend" below)

Then commit the regenerated `Api.ts` / `BApi2.d.ts` / `constants.ts`
together with the C# change.

## Decide where a new action runs

Every new controller action needs an answer to one question before it ships:
**does this do something on the machine the server runs on that only makes
sense on the user's own machine?** Launching a player or any other process,
opening a folder or a file in the desktop shell, showing a native window,
reading the local clipboard, capturing a login in an embedded browser.

It matters because the server is not always on the user's machine. In the
container build it never is, and a "play this" call from a remote browser
today starts a player on the server, on a screen nobody is watching, and
reports success.

- **Yes, it is user-side** — say so in the pull request. These actions are
  being collected so the client build can run them locally instead; the
  marker attribute for them arrives with that work. Until then, at minimum
  do not mark such an action `[RemoteAccessible]`.
- **No, it is ordinary data or file work** — mark it `[RemoteAccessible]`
  when a remote client legitimately needs it, and remember that path
  parameters need declaring so the path guard can check them.

Depending on `IGuiAdapter`, `ISystemPlayer`, `IBatchPlayService`,
`TampermonkeyService` or `IDLsiteWorkService` is a strong hint the answer is
"yes" — but the constructor is only a hint, so read the method body.

## Static data for the frontend

When the frontend needs a server-side value that is **known at build time**
(`static readonly` dicts, sets, lists — `InternalOptions.MediaTypeExtensions`
is the canonical example), do **not** add a runtime HTTP endpoint.

Extend
[`BakabaseConstantsGenerator`](../../src/Bakabase.Service/Components/BakabaseConstantsGenerator.cs)
so the value ships through `src/web/src/sdk/constants.ts`. The frontend then
imports it directly, no fetch, no loading state.

Use a runtime endpoint only when the value actually depends on runtime state
(DB, options, filesystem, user session, etc.).
