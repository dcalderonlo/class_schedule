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

POST returns 201 with the created projection. Invalid input returns 400, missing references 404, and duplicates 409 using problem JSON; rejected writes do not add catalog entries. Login and antiforgery failures follow the authentication API. Meetings, enrollment, capacity enforcement across meetings, and notifications are future work. No migrations are applied automatically.
