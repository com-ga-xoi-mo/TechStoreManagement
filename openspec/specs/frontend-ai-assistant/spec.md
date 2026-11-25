# frontend-ai-assistant Specification

## Purpose
In-app chat page for the read-only AI assistant, available to every role, keeping the conversation for the current sign-in session only.

## Requirements

### Requirement: Assistant destination for every role
The Shell SHALL show a "Trợ lý AI" destination to Admin, Manager, SalesStaff and WarehouseStaff. Adding it SHALL NOT change any role's landing page.

#### Scenario: Warehouse menu
- **WHEN** `warehouse1` signs in
- **THEN** the menu shows "Trợ lý AI" and Shell still opens Categories and Inventory

### Requirement: Chat page
The assistant page SHALL show the conversation, a message box and a Send button that is disabled while the box is empty or a reply is pending. While waiting it SHALL show a pending state and a Cancel button. Errors from the server SHALL be shown with the server message and a way to retry the last question. The conversation SHALL stay in memory for the current sign-in session, so leaving the page and coming back shows it again. It SHALL NOT be stored on disk and SHALL NOT be shown to the next session. A request still pending when the user leaves the page SHALL keep running; returning to the page SHALL show the pending state and then the reply or the error. Signing out or switching account SHALL cancel it and its reply SHALL NOT be shown. Messages SHALL NOT be selectable as list items. When the conversation is scrolled to the latest message, a new message SHALL scroll it into view; when the user has scrolled up, the view SHALL stay where it is and a "down arrow" button SHALL appear that jumps to the latest message. Opening the page SHALL show the latest message.

The assistant repository SHALL use a per-request POST timeout of 90 seconds through an explicit `IApiClient` overload. Existing callers SHALL retain their configured default timeout.

#### Scenario: Send and receive
- **WHEN** a user types a question and presses Send
- **THEN** the question appears, a pending state shows, and the reply appears when the server answers

#### Scenario: Cancel
- **WHEN** the user presses Cancel while waiting
- **THEN** the request is cancelled, the pending state ends and the next question is not sent with the cancelled one

#### Scenario: Server error
- **WHEN** the server returns 503, 502 or 504
- **THEN** the page shows the server message and the user can retry

#### Scenario: Follow new messages at the bottom
- **WHEN** the conversation shows the latest message and the user sends a question
- **THEN** the question and then the reply are scrolled into view

#### Scenario: Reading older messages
- **WHEN** the user has scrolled up and a reply arrives
- **THEN** the view does not move, a down arrow button appears, and pressing it scrolls to the latest message and hides the button

#### Scenario: Switch tabs
- **WHEN** a user chats, opens another Shell destination and returns to "Trợ lý AI"
- **THEN** the earlier questions and replies are still shown and the next question is sent with that history

#### Scenario: Leave while waiting
- **WHEN** a user sends a question, opens another Shell destination before the reply and returns
- **THEN** the question and the pending state are shown, and the reply appears when the server answers

#### Scenario: Sign out while waiting
- **WHEN** a request is pending and the user signs out
- **THEN** the request is cancelled and the next session does not show the question or its reply

#### Scenario: New session
- **WHEN** Admin chats, logs out and `cashier1` signs in
- **THEN** the assistant page opens empty for `cashier1`
