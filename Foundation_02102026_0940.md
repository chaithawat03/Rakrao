New requirement / change request:

The system must support a person's name history.

A Person may change:
- first name
- middle name
- last name
- full legal name

more than once during their lifetime.

The application must be able to display both the person's current name and previous names.

Example:

Current name:
ชัยธวัช ยาพรหม

Previous names:
- ชัยธวัช สมชาย
- ชัยวัฒน์ สมชาย

The UI should normally display the current name as the primary name.

Previous names should be available in the Person detail/profile page, for example:

Current name:
ชัยธวัช ยาพรหม

Formerly known as:
ชัยวัฒน์ สมชาย

The family tree node may optionally show a previous name in a compact form if appropriate, but it should not make the tree visually cluttered.

Important:

Do NOT simply add fields such as:

old_first_name
old_last_name

to the Person table.

A person can potentially have multiple historical names, so review whether a separate PersonNameHistory or PersonNames entity is more appropriate.

The design should consider storing information such as:

- PersonId
- FirstName
- MiddleName
- LastName
- FullDisplayName
- NameType
- EffectiveFrom
- EffectiveTo
- IsCurrent
- Notes

Also consider whether we should distinguish:

- LEGAL
- BIRTH
- FORMER
- PREFERRED
- ALIAS

Do not add unnecessary complexity if these distinctions are not currently required, but design the schema so they can be supported later without major restructuring.

Before implementing:

1. Review the existing domain model.
2. Identify all affected areas.
3. Explain whether the current database design can support this requirement.
4. Propose the schema change.
5. Identify required migration changes.
6. Identify affected API endpoints and DTOs.
7. Identify affected frontend components.
8. Identify effects on search.
9. Identify effects on family-tree node display.
10. Identify effects on audit logs and privacy rules.

Also consider search behavior.

Searching for a person's previous name should still be able to find that Person.

Example:

Current name:
ชัยธวัช ยาพรหม

Previous name:
ชัยวัฒน์ สมชาย

Searching for either name should return the same Person.

Update the relevant design documentation, including:

- docs/domain-model.md
- docs/database-design.md
- docs/api-design.md
- docs/ui-design.md

If this requirement changes any previous architectural assumptions, explicitly document the change.

Do not silently replace the existing design.

First provide:
- impact analysis
- proposed design
- files that will be changed

Then implement the change if there are no architectural conflicts.