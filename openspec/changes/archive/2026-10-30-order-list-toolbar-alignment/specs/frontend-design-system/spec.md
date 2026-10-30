# Spec Delta

## ADDED Requirements

### Requirement: Order list toolbar alignment
The Orders list SHALL present its title, primary create action, search field, filters, and reset action as a visually aligned toolbar. The search field and its clear action SHALL use a 40 DIP height on this page.

#### Scenario: View order list toolbar
- **WHEN** a user opens the Orders list
- **THEN** the title and description align with the reference; “Lập đơn mới” uses the shared primary button appearance and plus sign; filter labels sit above their controls; the search field, clear action, selectors, and reset action align within the filter panel.

#### Scenario: Search control on other pages
- **WHEN** a user opens another page that uses the shared search control
- **THEN** that page keeps its existing search-control height unless it explicitly sets a page-specific height.
