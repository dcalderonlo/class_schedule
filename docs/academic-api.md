# Academic catalog API

Authenticated users can list subjects, teachers, rooms, terms, offerings and sections at `/api/catalog/{resource}`. Only administrators can create them; obtain and send the antiforgery token as described in [authentication](authentication.md). No update or delete endpoints are provided.

## List and create

GET returns an array ordered by ID. `page` starts at 1; `pageSize` defaults to 20 and is limited to 1–100. Invalid or overflowing bounds return 400.

| Resource | POST JSON fields |
| --- | --- |
| subjects | `name`, `code`, `weeklyHours` |
| teachers | `firstName`, `lastName`, `email` |
| rooms | `number`, `capacity`, `type` (RoomType integer 0–4) |
| terms | `name`, `startDate`, `endDate` (inclusive ISO dates) |
| offerings | `subjectId`, `academicTermId` |
| sections | `courseOfferingId`, `code`, `capacity` |

Text is required and bounded (names 100, codes/room numbers 20, teacher email 200 characters). IDs, hours and capacities must be positive. Codes and room numbers are trimmed and uppercased; teacher emails are normalized for uniqueness. Term names need not be unique. An offering is unique per subject/term; section codes are unique within an offering. Teacher responses expose only ID and name, not email.

POST returns 201 with the created projection. Invalid input returns 400, missing references 404, and duplicates 409 using problem JSON; rejected writes do not add catalog entries. Login and antiforgery failures follow the authentication API.

## Weekly meetings

Authenticated users can GET `/api/schedule` or `/api/schedule/{id}`; only administrators can POST, PUT or DELETE, with the usual antiforgery token. The list uses the same bounded, ID-ordered pagination as the catalog. POST and PUT accept, for example:

```json
{"courseSectionId":1,"teacherId":1,"roomId":1,"dayOfWeek":1,"startTime":"09:00:00","endTime":"10:00:00"}
```

`dayOfWeek` is 0–6 (Sunday–Saturday). IDs are server-owned; responses include section, offering and subject identifiers and names, plus teacher and room IDs and display fields. This intentionally replaces the previous writable `subjectId` and `gradeLevel`: subject and term derive from the section's offering. Existing databases need an explicitly approved migration and data conversion; migrations are not applied automatically.

Times must form a positive same-day interval; midnight start is allowed, overnight is not. Terms are inclusive and the weekday must occur within the term. A section cannot overlap itself, and a room or teacher cannot overlap another meeting when the terms share an occurrence of that weekday. Adjacent intervals and resource reuse in nonoverlapping occurrences are allowed. Rooms must fit declared section capacity; weekly meeting duration cannot exceed the subject's weekly hours. Invalid input is 400, missing references 404, and conflicts 409. Conflicting concurrent writes may return a retryable 409; clients decide whether to retry. Validation and save use a serializable database transaction. SQLite file tests cover its provider only; SQL Server concurrency execution has not been performed. Enrollment and notifications remain future work.
