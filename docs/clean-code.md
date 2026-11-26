# Clean Code Rules

These rules follow the spirit of Robert C. Martin's *Clean Code*: code is read far more often
than it is written, so optimise for the reader. Some of the book's advice has aged — it was
written for Java in 2008 — so this document keeps the durable ideas, drops the dogma, and
applies them to this stack (C# / ASP.NET Core 10, React 18 + Vite, JS).

Numbers below are **smell thresholds**, not hard limits. Crossing one means "look again",
not "split mechanically".

---

## 1. Core principles

1. **Readable top to bottom.** A method or component reads like a short story: the public
   entry point first, the helpers it calls below it, each at one level of abstraction.
2. **Names carry the meaning.** If you need a comment to explain *what* code does, the
   name is wrong or a method is missing.
3. **One reason to change.** A class, method or component does one thing. "And" in a
   name (`ValidateAndSave`) is a signal to split.
4. **Make the right thing easy and the wrong thing hard.** Prefer immutable data, narrow
   types and guard clauses over defensive checks scattered through the body.
5. **Leave it cleaner than you found it** — but in its own commit (see §9).

## 2. What changed since the 2008 book

| Old advice | Current rule here | Why |
|---|---|---|
| Functions should be 2–4 lines | Extract when the new **name adds information**. ~20 lines is a smell threshold, not a target. | Over-fragmentation makes readers jump between dozens of one-liners. Cohesion beats size. |
| Never more than 0–2 arguments | Up to 3 is fine. Beyond that, introduce a parameter object / `record`. Avoid boolean flag arguments. | Named arguments and records make a few parameters readable; flags still hide two functions in one. |
| "Comments are always a failure" | No comments **inside method bodies**. Short *why* docs at type/member level are fine. | Intent, contracts and non-obvious business rules belong to the API, not to individual lines. |
| Inheritance hierarchies, abstract base classes | Composition, small interfaces, pure functions. | Deep hierarchies couple more than they share. |
| An interface for every class | An interface where there is a real seam: DI-registered services, repositories, anything mocked in tests. | Interfaces with one impl and no test seam are noise. |
| Mutable objects with getters/setters | Immutable `record`s and `init` properties for data; mutation only inside entities/aggregates. | Less state to track while reading. |
| Exceptions everywhere | Domain exceptions for rule violations; `null` / `Try…` / result values for expected "not found" paths. | Exceptions are for exceptional flow, not branching. |

## 3. Naming

- **Intention-revealing, pronounceable, searchable.** `scoringDay`, not `sd`; `pageItems`,
  not `list`. Single letters only for trivial lambdas (`s => s.Id`) and loop indices.
- **No abbreviations** that aren't domain language. `_submissions`, not `_subs`;
  `_screenshotUrlSigner` or `_signer` is fine because the type says the rest.
- **Booleans read as questions:** `isWinner`, `hasSubmittedForLatest`, `canSeeDay`.
- **Methods are verbs, types are nouns.** `ComputeWinnersByDayAsync`, `RankingStrategy`.
- **Avoid noise words:** `Manager`, `Helper`, `Util`, `Data`, `Info` say nothing. Prefer
  the responsibility: `ScoringDayCalculator` over `ScoringDayHelper`.
- **One word per concept** across the codebase: pick `Get` *or* `Fetch` *or* `Load`.
  Here: `Get…Async` for reads, `Create/Update/Delete…Async` for writes.
- **C#:** `PascalCase` types/members, `_camelCase` private fields, `camelCase` locals,
  `Async` suffix on every awaitable method, `I` prefix on interfaces.
- **React:** components `PascalCase`, hooks `useSomething`, event props `onSomething`,
  handlers `handleSomething`.

## 4. Functions and methods

- **Do one thing at one level of abstraction.** A method either orchestrates (calls
  well-named steps) or does detailed work — not both.
- **Stepdown rule:** public method first, private helpers directly below in call order.
- **Guard clauses over nesting.** Return early; keep nesting depth ≤ 2.
- **No flag arguments.** `GetPage(…, bool hideScores)` → two methods or a strategy.
- **No hidden side effects.** A `Get…` method does not write, a `Compute…` method does not
  persist, a `Validate…` method does not mutate.
- **Command–query separation:** a method changes state *or* returns information.
- **Prefer expressions** where they stay readable: expression-bodied members, `switch`
  expressions, pattern matching, LINQ over manual loops that build collections.
- **Parameters:** ≤ 3; more → a `record` (`SubmissionQuery`). Optional parameters only at
  API boundaries. A trailing `CancellationToken ct` is plumbing and doesn't count.

## 5. Comments

**Rule: no comments inside method or component bodies.** When you feel the urge, refactor:

| Instead of… | Do this |
|---|---|
| `// Compute winners for the page` above a block | Extract `ComputeWinnersForPage(...)` |
| `// 86400 seconds = 1 day` | `TimeSpan.FromDays(1)` or a named constant |
| `// only admins or submitters may see scores` | `bool canSeeScores = await _visibility.CanSeeDayAsync(...)` |
| Commented-out code | Delete it. Git remembers. |
| `// Arrange` / `// Act` / `// Assert` in tests | Separate the three phases with blank lines |

**Allowed (outside bodies):**

- `///` XML docs on public contracts in `Services/Contracts/` and repository interfaces:
  what it guarantees, what it throws, units.
- A short *why* above a type or member for non-obvious business rules, legal constraints
  or workarounds — with a link to the issue/spec when there is one.
- JSDoc on exported hooks and shared components when the props/return shape is not obvious.

**Allowed inside bodies (the only exceptions):**

- Analyzer/linter suppressions with a reason:
  `// eslint-disable-next-line react-hooks/exhaustive-deps -- runs once on mount by design`
  `#pragma warning disable CA2000 // ownership transferred to caller`
- Regex or algorithm references that cannot be expressed by naming (link to the source).

## 6. Classes and services (C#)

- **Small, cohesive classes.** > ~300 lines or > ~5–7 constructor dependencies means the
  class has more than one responsibility. Split by use case (e.g. `SubmissionQueryService`
  and `SubmissionCommandService`), not by technical layer.
- **Primary constructors** for DI services; do not reassign the captured parameters.

  ```csharp
  public sealed class FavoriteService(IFavoriteRepository favorites, TimeProvider time)
      : IFavoriteService
  {
      public async Task<bool> IsFavoriteAsync(int userId, int gameId, CancellationToken ct) =>
          await favorites.ExistsAsync(userId, gameId, ct);
  }
  ```

- **File-scoped namespaces** (`namespace DailyChallenges.Services;`) in new and touched files.
- **`sealed` by default.** Unseal when there is a real reason to inherit.
- **`record` for DTOs** and value objects; `init` over `set` where EF allows it.
- **Nullable reference types are on** — never silence them with `!` unless you can name
  the invariant that guarantees non-null (and then encode it in a type instead).
- **Law of Demeter:** don't reach through objects (`game.Owner.Profile.Settings.Theme`).
  Ask for what you need.
- **Layering stays one-directional:** Controller → Service → Repository → `AppDbContext`.
  Controllers hold no business logic; services do not return `IActionResult`; only
  repositories touch `DbSet`s. (Existing direct `AppDbContext` use in services is
  refactoring debt, not a pattern to copy.)
- **Time and randomness are injected** (`TimeProvider`, not `DateTime.UtcNow`).
- **Async all the way:** no `.Result` / `.Wait()`; pass `CancellationToken` through from
  controllers to EF calls.
- **No magic numbers/strings:** constants, `enum`s or options classes bound from config.

## 7. Error handling

- **Throw domain exceptions** from `backend/Exceptions/DomainExceptions.cs`;
  `ExceptionToHttpResponseMiddleware` maps them to HTTP. Don't build error responses by hand
  in controllers.
- **Expected absence is not an error:** repositories return `null` (or `TryGet…`); the
  service decides whether that is a `NotFound` domain exception.
- **Don't catch what you can't handle.** No `catch (Exception) { }`; no logging-and-rethrow
  at every layer. Catch at boundaries (middleware, background service loop).
- **Fail fast at the edge:** validate input in DTO validation / guard clauses at the top of
  the service method, then trust it inside.
- **Frontend:** API errors are handled once in `src/api/` / the calling hook and surfaced via
  the Snackbar context; components don't each invent their own error UI.

## 8. React / frontend

- **Components render, hooks think.** Data fetching, derived state and side effects live in
  custom hooks in `src/hooks/`; components compose hooks and return JSX.
- **Component size:** > ~150 lines or more than one `useEffect` doing unrelated things →
  extract subcomponents or hooks. Page components are composition roots, not monoliths.
- **Derive, don't sync.** If a value can be computed from props/state, compute it during
  render (or `useMemo` when measurably expensive) — don't mirror it into state via `useEffect`.
- **No inline logic in JSX** beyond a simple condition. Name it:
  `const canInvite = isOwner && !league.isFull;` then `{canInvite && <InviteButton />}`.
- **Props:** destructure in the signature; ≤ ~5 props, otherwise the component does too much
  or wants a grouped object.
- **Styling:** use tokens from `src/styles/tokens.js` and the MUI theme; no magic colours
  or pixel values in components.
- **No dead code:** remove unused imports, props and branches; the linter warns on them.

## 9. Tests

- **F.I.R.S.T.:** Fast, Independent, Repeatable, Self-validating, Timely.
- **Names state behaviour:** `Method_Scenario_ExpectedResult` (C#, already used) or
  `it('shows an error when the upload is too large')` (Vitest).
- **One behaviour per test**, Arrange / Act / Assert separated by blank lines.
- **Test through public APIs.** Don't make members `internal`/public just to test them —
  that's a sign the class wants splitting.
- **Builders/factories over copy-pasted setup.** Test code is code; the same rules apply.
- **Frontend:** query by role/label (Testing Library), assert on what the user sees, not on
  internal state.

## 10. Refactoring workflow

1. **Cover before you cut.** If the code you're touching has no tests, add characterization
   tests first that pin the current behaviour.
2. **Small, behaviour-preserving steps** — rename, extract method, extract class, move —
   running tests after each.
3. **Separate refactoring from behaviour changes.** Use `refactor:` commits (conventional
   commits; they don't trigger a release). A `feat:`/`fix:` commit should not also
   reshuffle unrelated code.
4. **Keep public API and DB schema stable** during a refactor. Renaming DTO fields or
   endpoints is a breaking change, not a refactor.
5. **Boy Scout rule, scoped:** clean what you touch for your change; open a separate PR for
   wider cleanups.

## 11. Review checklist

- [ ] Can I understand each method without scrolling or reading its helpers?
- [ ] Any comment inside a body that should be a name instead?
- [ ] Any method with > 3 parameters, boolean flags, or nesting > 2?
- [ ] Any class with > ~7 dependencies or > ~300 lines? Any component > ~150 lines?
- [ ] Any magic number/string, `DateTime.Now`, `.Result`, or swallowed exception?
- [ ] Do names use the domain language (scoring day, submission, league, streak)?
- [ ] Are refactors and behaviour changes in separate commits, with tests green?
