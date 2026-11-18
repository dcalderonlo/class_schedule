# Enrollment and internal notifications

Authenticated users select a course section, not individual weekly meetings. Administrators can assign sections and edit meetings; affected users receive internal notifications. All writes require the existing antiforgery token and Identity cookie.

## Quick path

1. Fetch `GET /api/account/csrf` and send its token in `X-CSRF-TOKEN` on writes.
2. Use `POST /api/enrollments` with `{ "courseSectionId": 1 }` to select a scheduled section.
3. Use `GET /api/enrollments` or `GET /api/enrollments/timetable` to inspect your selections and meetings. Use `PUT /api/enrollments/{id}` with the same body to switch sections in that offering.

## Routes and boundaries

| Route | Access | Result |
| --- | --- | --- |
| `GET /api/enrollments`, `/timetable` | Authenticated | Own assignments or meeting projections |
| `GET /api/enrollments/{id}` | Owner or administrator | Assignment; other users receive 404 |
| `POST /api/enrollments` | Authenticated | Create own assignment; duplicate offering returns 409 |
| `PUT /api/enrollments/{id}` | Owner or administrator | Switch within the same offering, retaining the assignment ID |
| `GET /api/enrollments/users` | Administrator | Bounded user IDs and email addresses only |
| `GET /api/enrollments/users/{userId}` | Administrator | User's assignments |
| `POST /api/enrollments/users/{userId}` | Administrator | Create assignment using `{ "courseSectionId": 1 }` |
| `GET /api/notifications` | Authenticated | Own notifications, newest ID first |
| `POST /api/notifications/{id}/read` | Owner only | Mark read idempotently; other users, including administrators, receive 404 |

Lists default to `page=1&pageSize=20` and accept at most 100 rows per page. Invalid or overflowing bounds return 400. User IDs and roles in request bodies are not authority. Cookie authentication returns 401 for anonymous requests; administrator-only routes return 403 for students. In production, an unexpected API failure returns 500 with the generic JSON error `An unexpected error occurred.` rather than exposing internal details.

## Rules and delivery limits

A section must have at least one meeting and a free seat. One user cannot enroll twice in the same offering or choose meetings that overlap on an actual shared term weekday. A rejected switch keeps the original assignment. Administrators cannot edit a meeting into a conflict for enrolled users. Concurrent conflicts return 409; clients may retry after refreshing state.

An administrator assignment or actual switch creates one internal notice for the affected user. Meeting creation, actual changes and deletion notify distinct users in affected sections. Enrollment or meeting changes and their notices are saved in one database transaction. Students' own choices do not generate notices. There is no external delivery, withdrawal, or background worker.

Relational integration tests use isolated SQLite files. SQL Server locking, deployment configuration and migrations require separate verification before deployment; no migration is applied by this feature.
