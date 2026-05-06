# AGENTS

## Priorities
- Keep it simple.
- Keep patterns consistent across the codebase.
- Prefer tidy, readable code over clever code.
- Avoid over-engineering; this is not an enterprise multi-project solution.

## TypeScript / JavaScript
- Treat `wwwroot/js/*.js` as generated output.
- Do not manually edit generated JS files.
- Make frontend logic changes in `Scripts/*.ts`.
- Prefer the existing global jQuery helpers/style for DOM events, AJAX, and small UI updates when it keeps the code tidy.
- Compile TS with `npm run build` from the repo/project root.

## C# / Data / LINQ
- Reduce query overhead; avoid unnecessary `Include` calls.
- Avoid redundant work in queries and mappings.
- When querying nullable values, prefer clean expressions and avoid unnecessary `.Value` usage.
- Extract extension methods for repeated transformations/logic.
- For read-only pages/API payloads, prefer narrow projections to view models over materializing entity graphs.
- Keep custom in-memory ordering where EF cannot express the rule clearly (for example Discogs track positions).
- I prefer 'x' in LINQ selecting/where clauses etc

## MVC / Razor / UI
- Keep markup style and naming consistent across pages.
- Use partials for shared/repeated UI instead of duplicating markup.
- Keep partial/page composition clean and intentional.
- `Index.cshtml` is the default page/action convention; keep it unless a rename clearly improves navigation.
- Controllers should mostly handle HTTP flow: auth/user lookup, model-state handling, status messages, redirects, and returning views/results.
- Move orchestration and data shaping into services rather than growing controllers.
- Keep `ApplicationController` thin: shared HTTP helpers only, no service dependencies or data access.

## Project Structure
- `Controllers/` contains MVC controllers plus `ApplicationController` for shared controller conveniences.
- `Services/` root contains app-facing services. Supporting service types live in:
  - `Services/Interfaces/` for service contracts.
  - `Services/Background/` for hosted/background workers.
  - `Services/Queues/` for background queue implementations.
  - `Services/Discogs/` for Discogs-specific integration/support.
  - `Services/LastFm/` for Last.fm-specific integration/scrobbling.
  - `Services/Settings/` for Settings page orchestration.
  - `Services/Caching/` for cache keys/entries.
  - `Services/Utilities/` for small stateless helpers.
- `Models/` is grouped by feature (`Collection`, `Catalog`, `Stats`, `Tracks`, `Home`, `Settings`, `Shared`) while keeping the `DiscogScrobblerMVC.Models` namespace.
- Prefer `ViewModel` names for MVC/data-to-view shapes. Avoid new `Dto` names unless a true external transport contract appears.

## Code Quality
- Remove dead, redundant, or unused code.
- Keep interfaces in their own files.
- Maintain separation of concerns without adding unnecessary layers.
- Keep services cohesive; avoid spaghetti dependencies.
- Use self-documenting names (variables, methods, classes).
- Keep comments minimal and useful; no verbose or obvious comments.
- Prefer structure that helps navigation without creating folders for every tiny concept.
