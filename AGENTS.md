# Repository engineering rules

Read [docs/product-decisions.md](docs/product-decisions.md) and the relevant design documents before changing behavior or schema. Keep the approved modular monolith and technology stack unless a measured requirement and reviewed decision change them. Do not silently replace architectural decisions; update the decision record and affected documents when a decision changes.

## Product naming

- The official user-facing Thai name is `รากเรา`; the Romanized brand is `RAKRAO` (Our Roots). Use `RakRao` for the .NET solution, projects and namespace prefix, and `rakrao` for lowercase identifiers such as package names and URL slugs.
- Planned solution/projects are `RakRao.sln`, `RakRao.Api`, `RakRao.Domain`, `RakRao.Application`, and `RakRao.Infrastructure`; the frontend package is `rakrao-web`. Do not scaffold them merely to satisfy a documentation task.
- Keep generic domain names such as Family, Person, FamilyMembership, FamilyAdmin and FamilyPerson, along with `/api/v1` routes and domain table names. Branding does not justify renaming them.
- Treat cloud/Firebase project IDs, buckets, service names, deployed URLs and OAuth settings as external identifiers. Inventory dependencies and obtain separate authorization before changing existing resources.
- The working tagline is “Discover how we are connected.” It may change and is not an architectural constant.
## Domain and data

- Person and User are separate concepts. FamilyMembership and FamilyPerson association are separate too. Membership approval does not prove a User's Person identity.
- Person name changes preserve history in PersonName records. Never add `old_name`, `old_first_name`, or similar one-off fields. Search former names only when the caller can read them.
- Never store one global generation number on Person. Compute generation for a family, root and lineage policy.
- Never store derived sibling or marital-status fields. Derive them from typed relationship history.
- A Person may belong to several Families. Shared core Person edits must pass the affected-family policy; local aliases belong to FamilyPerson.
- Cross-family relationships stay unpublished until every affected Family authorizes publication. An edge never grants access to another Family.
- All tree traversal is bounded by configurable depth, node count and execution timeout, with cycle detection.
- Database schema changes require migrations and a reviewed data/backfill strategy. Important mutations require audit events without leaking hidden names, tokens or sensitive evidence into broad logs.

## Security and privacy

- Security enforcement belongs in the API. Treat every request as untrusted and verify Firebase ID tokens server-side.
- Never authorize from frontend-supplied roles or treat a client-supplied FamilyId as proof of authorization. Resolve current memberships, scoped grants and resource visibility on the backend.
- Hidden names must not influence unauthorized search results, match hints or tree output.
- The MVP is private-first: no global public directory of people, families or relationships and no public person search. Living-person data defaults to FAMILY_MEMBER visibility.
- Family creation by an authenticated User transactionally creates the Family, active creator membership, Creator and FamilyAdmin grants, and an audit event. Never remove the last active Creator.
- Joining a Family and claiming a Person are separately reviewed operations. Name, former name, phone or email matches alone never prove identity. Do not implement automatic claiming for minors or invent a guardian ownership model in Phase 1.

## Changes and verification

1. Evaluate new requirements against the domain model before patching UI or database. Avoid one-off fields for repeatable or historical concepts.
2. Check effects on database, migrations, API, UI, authorization, privacy, search, audit and tests. Preserve backward compatibility where practical.
3. Explain the impact and proposed design before implementation. Update relevant documentation when assumptions change.
4. Run appropriate verification before claiming work is complete. For documentation-only changes, check requested files, internal links, resolved decisions and contradictions.