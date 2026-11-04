# Feature Specification: Saga-Sales Multi-Tenant E-Commerce Platform

**Feature Branch**: `001-saga-sales-ecommerce`

**Created**: 2026-11-04

**Status**: Draft

**Input**: User description: "Full e-commerce platform with catalog management, sales pipeline, payment processing, customer management, analytics dashboards and notification services, multi-tenant SaaS, for merchants who need to sell online and see their results."

**Business context**: this specification is the technical counterpart of
[`epics.md`](./epics.md), which holds the business epics, their indicators and the release
roadmap. Where both documents speak about scope, `epics.md` §6 (roadmap) governs priority and
this document governs behaviour.

## Clarifications

### Session 2026-11-04

- **Q**: Does a product carry a single price for everybody, or several price lists per customer type? → **A**: Price lists per customer type (retail / wholesale).
- **Q**: One single currency or multiple currencies with rates? → **A**: One single currency, USD.
- **Q**: Is the shipping address always required? → **A**: Always required, regardless of the kind of product.

### Session 2026-11-04 (actors and permissions)

- **Q**: Is the buyer a sign-in account or an anonymous participant? → **A**: In the MVP the buyer is a customer record created by store staff, and orders are placed by staff on their behalf. A self-service buyer account arrives with the storefront (Story 10, P3).
- **Q**: Does a dedicated sales role exist? → **A**: Yes. It places orders and records payments; it cannot edit prices, refund, or manage users.
- **Q**: May the platform operator read business data? → **A**: No. The operator sees service status, logs and metrics only; audited impersonation is out of scope for the MVP.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open a store and sign in (Priority: P1) · E-01 · US-017

A merchant registers their store with a name and an administrator account, then signs in and
lands in their own workspace. From that moment everything they see belongs to their store only.

**Why this priority**: nothing else can be used without a store and an identity. It is the
foundation of the multi-tenant model and the first thing a paying customer does.

**Independent Test**: register a store, sign in with the administrator credentials, and confirm
that a second store's data is invisible to the first one. Delivers the whole access model.

**Acceptance Scenarios**:

1. **Given** an email that is not registered, **When** the merchant registers a store, **Then** the store exists in trial state with an administrator who can sign in.
2. **Given** two stores with their own users, **When** a user of store A requests a resource of store B, **Then** the request is refused and no data of store B is returned.
3. **Given** a wrong password, **When** the user tries to sign in, **Then** the sign-in fails with a generic message that does not reveal whether the email exists.

---

### User Story 2 - Publish and maintain the catalogue (Priority: P1) · E-02 · US-001, US-002

A manager creates products with a name, a description, a price and available stock, organises
them in categories, and keeps them up to date as the assortment changes.

**Why this priority**: without a catalogue there is nothing to sell. It is the first economic
asset of the store.

**Independent Test**: create a product, list the catalogue filtered and sorted, update the price,
retire the product. Delivers a usable catalogue on its own.

**Acceptance Scenarios**:

1. **Given** a manager signed in to their store, **When** they create a product with a name of at least two characters, a positive price and a non-negative stock, **Then** the product is created and immediately listed.
2. **Given** a catalogue with 30 products in one category, **When** the manager requests the second page of ten, **Then** ten products are returned and the reported total is 30.
3. **Given** a product that has already been sold, **When** the manager retires it, **Then** the product disappears from the catalogue while the historical orders still show its name and price.
4. **Given** a store with categories, **When** a manager tries to create a duplicated category name in the same store, **Then** the creation is rejected with a clear message.
5. **Given** a product with a base price of 100 and a wholesale price of 80, **When** a wholesale customer browses the catalogue, **Then** the price shown is 80, while a retail customer sees 100.

---

### User Story 3 - Place an order with stock reserved (Priority: P1) · E-03 · US-004, US-006, US-007

A buyer selects products and quantities and receives an order confirmation. The order keeps the
price and name of each product as they were at that moment, and the stock is decremented so the
same unit cannot be sold twice.

**Why this priority**: this is the moment revenue is created. Getting stock wrong here destroys
trust immediately.

**Independent Test**: place an order for available stock and verify the stock drops; repeat with
insufficient stock and verify the order is refused and the stock is untouched.

**Acceptance Scenarios**:

1. **Given** a product with 10 units in stock, **When** a buyer orders 2 units, **Then** the order is created in Pending state and the stock becomes 8.
2. **Given** a product with 1 unit in stock, **When** three buyers order 1 unit at the same time, **Then** exactly one order succeeds, the other two are refused, and the stock ends at 0, never negative.
3. **Given** a product whose price changes after an order, **When** that order is consulted, **Then** it still shows the price and name captured when it was placed.
4. **Given** an order attempt that fails halfway through, **When** the failure is reported to the buyer, **Then** any stock already reserved by that attempt has been released.

---

### User Story 4 - Move an order through its life cycle (Priority: P1) · E-03 · US-005

Store staff advance an order from Pending to Confirmed, Processing, Shipped and Delivered, or
cancel it, and the buyer can see where it stands.

**Why this priority**: a confirmed order cannot be fulfilled without a controlled state machine.
Illegal transitions are the main source of operational errors.

**Independent Test**: advance one order along the whole chain and try an illegal jump; the first
succeeds, the second is refused and the state does not change.

**Acceptance Scenarios**:

1. **Given** a Pending order, **When** staff moves it forward one step at a time, **Then** each transition is accepted in order up to Delivered.
2. **Given** a Pending order, **When** staff tries to move it directly to Delivered, **Then** the change is refused and the order stays Pending.
3. **Given** a Cancelled order, **When** staff tries to confirm it, **Then** the change is refused because Cancelled is final.
4. **Given** an order from another store, **When** staff tries to change its state, **Then** the request is refused.

---

### User Story 5 - Record a payment received outside the platform (Priority: P1) · E-04a · US-018

Until online payment is enabled, staff records that an order was paid by transfer, deposit or
cash, with its amount and date, and the order is confirmed.

**Why this priority**: it is the MVP's only way to close the loop between order and money, and it
is what lets the first real merchant operate.

**Independent Test**: record a manual payment on a Pending order and verify the order becomes
Confirmed with the paid amount registered.

**Acceptance Scenarios**:

1. **Given** a Pending order of 199.98, **When** staff records a manual payment of 199.98, **Then** the order becomes Confirmed and the paid amount is 199.98.
2. **Given** an order that is already paid, **When** staff records a second manual payment for it, **Then** the second record is rejected.
3. **Given** a manual payment of zero or a negative amount, **When** staff tries to record it, **Then** it is rejected.

---

### User Story 6 - Manage customers and find them quickly (Priority: P2) · E-05 · US-011, US-012

An administrator registers customers with their contact details and addresses, and any staff
member can find one by email or phone in seconds while attending a call.

**Why this priority**: it speeds up daily support, but the store can sell without it in the first
release.

**Independent Test**: create two customers, search by phone and by email, and confirm there are
no duplicates inside the same store.

**Acceptance Scenarios**:

1. **Given** a store with customers, **When** staff searches by phone, **Then** the matching customer appears in under one second.
2. **Given** an existing email in the same store, **When** staff creates another customer with it, **Then** the creation is rejected, but the same email is allowed in a different store.
3. **Given** a customer with orders, **When** staff deletes them, **Then** the customer stops appearing in lists but their orders keep their history.

---

### User Story 7 - Keep the buyer informed (Priority: P2) · E-07 · US-015, US-016

The buyer receives an order confirmation without the store having to send it by hand, and the
operator is alerted when an unusually large order arrives.

**Why this priority**: it removes manual work and prevents "did my order go through?" calls, but
it does not block the sale.

**Independent Test**: place an order and verify the confirmation is recorded and dispatched;
verify a failed notification does not break the order.

**Acceptance Scenarios**:

1. **Given** a buyer with an email address, **When** the order is confirmed, **Then** the confirmation is dispatched within 30 seconds.
2. **Given** a notification channel that is down, **When** an order is placed, **Then** the order still succeeds and the failed notification is recorded with its error.
3. **Given** an order above the configured high-value threshold, **When** it is placed, **Then** the operator receives an alert.
4. **Given** an order whose buyer has no email address, **When** it is placed, **Then** the confirmation is skipped and the reason is recorded, without failing the order.

---

### User Story 8 - Collect and refund payments online (Priority: P2) · E-04b · US-008, US-009, US-010

A buyer pays with a card or another online method, the store sees the payment authorised, and
staff can refund all or part of it later.

**Why this priority**: it removes manual reconciliation, but it is not needed for the first
merchant to sell.

**Independent Test**: authorise a payment for an order, refund it partially and then completely,
and verify the amounts and states.

**Acceptance Scenarios**:

1. **Given** an unpaid order, **When** a payment is authorised, **Then** the payment is recorded as authorised with its provider reference.
2. **Given** a payment of 50, **When** staff refunds 30, **Then** the payment stays partially refunded and 20 remains refundable.
3. **Given** a payment of 50, **When** staff tries to refund 60, **Then** the refund is refused and the payment is unchanged.
4. **Given** a notification from the payment provider whose authenticity cannot be established, **When** it arrives, **Then** no payment is modified.

---

### User Story 9 - Understand the business and export it (Priority: P3) · E-06 · US-013, US-014

The merchant opens a daily view of revenue, order count and best-selling products, filters it by
date range, and exports it to a spreadsheet for their accountant.

**Why this priority**: it is what makes the platform valuable over time, but the store can trade
without reports for a while.

**Independent Test**: generate orders on known dates, query the summary for that range, verify
the totals, and export the same data as a spreadsheet-friendly file.

**Acceptance Scenarios**:

1. **Given** orders on known dates, **When** the merchant queries a date range, **Then** revenue, order count and top products match those orders exactly.
2. **Given** a range of seven days, **When** the merchant exports it, **Then** the file opens in a spreadsheet with one row per day and no manual steps.
3. **Given** a range with no movements, **When** the merchant exports it, **Then** the file contains headers only and the request does not fail.
4. **Given** two stores, **When** a merchant of store A queries the range, **Then** only store A's figures are returned.

---

### User Story 10 - Buy through the storefront (Priority: P3) · E-09 · US-003

A buyer browses the real catalogue, sees availability, places an order and receives confirmation
without the merchant intervening.

**Why this priority**: it completes the self-service experience, but the first merchants can take
orders by other means.

**Independent Test**: browse the catalogue, see a product's availability, complete an order and
reach the confirmation page.

**Acceptance Scenarios**:

1. **Given** a store with products, **When** a buyer opens the catalogue, **Then** only that store's available products are shown with their real stock.
2. **Given** a buyer who has completed an order, **When** the order is accepted, **Then** a confirmation is shown with the order identifier and total.
3. **Given** a product that just went out of stock, **When** another buyer tries to order it, **Then** the order is refused with a clear message.

---

### Edge Cases

- The last unit of a product is ordered by two buyers at the same instant: one succeeds, the other is told it is out of stock, and stock never goes below zero.
- A buyer keeps a product in the cart while the manager raises its price: the order keeps the price shown when it was confirmed, and the difference is never silent.
- An order fails after part of its stock was reserved: the reservation is released so the units are sellable again.
- The payment provider sends the same confirmation twice: the second one does not duplicate or double-count the payment.
- A refund is requested after a partial refund: it cannot exceed what remains refundable.
- A product has no price defined for the buyer's customer type: the base price applies and the buyer is told which price was used.
- A customer has no customer type assigned: they are treated as retail, never as an error.
- A buyer's customer type changes between browsing and confirming the order: the order uses the type in force at confirmation, and the applied price is reported.
- A store with no products opens the catalogue: an empty state is shown, never an error.
- A suspended or cancelled store tries to operate: the request is refused with a clear reason.
- A product name or search term contains accents or special characters: search still finds it.
- The notification channel is unavailable at checkout: the order succeeds and the failure is recorded.
- A report is requested for a range that starts after it ends: the request is rejected with a clear message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The platform MUST let a merchant register a store with a name, an administrator email and a password, creating the store in trial state.
- **FR-002**: Users MUST sign in with email and password and receive a credential that identifies them and their store.
- **FR-003**: The platform MUST support the roles administrator, manager, sales and customer, and restrict operations by role. The customer role names the buyer; in the MVP it is not a sign-in role (see Clarifications).
- **FR-003a**: The sales role MUST be able to place orders, record manual payments and consult the catalogue and customers; it MUST NOT edit prices, refund payments or manage users.
- **FR-003b**: The platform operator MUST NOT read store business data by default; operator access is limited to service status, logs and metrics. Audited impersonation is out of scope for the MVP.
- **FR-004**: Every request for store data MUST be resolved to exactly one store; a request without a resolvable store MUST be rejected instead of defaulting.
- **FR-005**: A store MUST never be able to read or modify another store's data.
- **FR-006**: Managers MUST be able to create, update and retire products with a name of at least two characters, a positive price, a non-negative stock and an optional category.
- **FR-007**: Every product MUST have a base price and MAY define additional prices per customer type (retail, wholesale).
- **FR-007a**: Every customer MUST carry a customer type; when it is not set, retail applies.
- **FR-007b**: The price applied to an order line MUST be the one corresponding to the buyer's customer type at the moment the order is placed; when that type has no specific price, the base price applies and the fallback MUST be visible to the buyer.
- **FR-008**: Categories MUST be hierarchical inside a store and unique by name inside that store.
- **FR-009**: The catalogue MUST be listed with pagination, filtering by category, text search by name, and ordering by name or price.
- **FR-010**: Retiring a product MUST NOT erase it from historical orders, which keep the name and price captured at purchase time.
- **FR-011**: Placing an order MUST reserve stock atomically; when the stock is insufficient the order MUST be refused with a clear error and the stock MUST remain unchanged.
- **FR-012**: Each order line MUST capture the product name, the unit price and the quantity at the moment the order is placed.
- **FR-013**: An order MUST follow the states Pending, Confirmed, Processing, Shipped and Delivered, with Cancelled and Refunded as final states; any other transition MUST be refused.
- **FR-014**: A checkout attempt that fails after reserving stock MUST release those reservations.
- **FR-015**: Staff MUST be able to record a payment received outside the platform (amount, date, reference), which confirms the order.
- **FR-016**: A payment MUST NOT be recorded twice for the same order.
- **FR-017**: Refunds MUST be allowed totally or partially and MUST NOT exceed the amount that remains refundable.
- **FR-018**: Buyers MUST be able to pay online with the payment methods the store enables, recording the provider reference and its raw answer.
- **FR-019**: Notifications received from the payment provider MUST only modify a payment when their authenticity can be established.
- **FR-020**: Customers MUST be unique by email inside a store and MUST be searchable by email, phone or name.
- **FR-021**: Deleting a customer MUST be logical: they stop appearing in lists while their orders keep their history.
- **FR-022**: The platform MUST dispatch an order confirmation to the buyer without the store sending it manually.
- **FR-023**: A notification failure MUST NOT prevent or undo an order, and MUST be recorded with its error.
- **FR-024**: The platform MUST alert the operator when an order exceeds a configurable high-value threshold.
- **FR-025**: The platform MUST expose daily sales figures per store (revenue, number of orders, successful payments) and the best-selling products for a date range.
- **FR-026**: Reports MUST be exportable in a format that opens directly in a spreadsheet, with one row per day.
- **FR-027**: All monetary amounts MUST be recorded in a single platform currency (USD) with two decimals; multi-currency and conversion are out of scope.
- **FR-028**: Every order MUST require a shipping address, regardless of the kind of product sold.
- **FR-029**: Each service MUST expose a status endpoint that reports its own health and the health of its dependencies.
- **FR-030**: The platform MUST keep serving reads when a messaging dependency is unavailable, reporting it as degraded.

### Key Entities

- **Store**: the tenant. Owns everything else; has a name, a public identifier, a plan, a state and an optional custom domain.
- **User**: a person who signs in. Belongs to one store, has an email, a password, a role and an active flag.
- **Product**: what the store sells. Name, description, price, currency, available stock, state and optional category.
- **Category**: grouping of products, hierarchical inside one store and unique by name per store.
- **Order**: a purchase. Belongs to a store and a customer, carries a state, a subtotal, the paid amount, a shipping address and dates.
- **Order line**: one product inside an order, with the name, unit price and quantity captured at purchase time plus the stock available at that moment.
- **Payment**: money received for an order. Belongs to a store and an order, with a method, a state, an amount, a refunded amount and the provider reference.
- **Customer**: a buyer of the store. Name, email, phone, customer type (retail or wholesale), state and addresses; unique email per store.
- **Price list**: the set of specific prices a store defines for one customer type. When a product has no entry for the buyer's type, the product's base price applies.
- **Address**: a customer's delivery location.
- **Daily sales summary**: per store and date, revenue, order count, successful payments and best-selling products.
- **Notification record**: an attempt to inform someone. Type, recipient, subject, state, error and timestamps.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A merchant can go from registration to their first successful sign-in in under 10 minutes without help.
- **SC-002**: A manager can publish a product and see it available in the catalogue in under 2 minutes.
- **SC-003**: When three buyers compete for the last unit of a product, exactly one order succeeds and stock never becomes negative, in 100% of attempts.
- **SC-004**: 95% of catalogue searches and listings return their results in under one second for a store with 50 products.
- **SC-005**: A buyer can complete an order in under 3 minutes and in no more than 5 steps.
- **SC-006**: 100% of orders show the price and name captured at purchase, even after the product is edited or retired.
- **SC-007**: The order confirmation reaches the buyer within 30 seconds of the order being placed.
- **SC-008**: Zero cross-store data exposures across all acceptance tests run with two stores.
- **SC-009**: The previous day's sales figures are available before 06:00 local time and match the recorded orders in 100% of cases.
- **SC-010**: The daily report opens in a spreadsheet with one row per day and requires no manual conversion.
- **SC-011**: 90% of stores complete their first order within 24 hours of creating their store.
- **SC-012**: In 100% of orders the price charged matches the buyer's customer type at confirmation, and every fallback to the base price is visible to the buyer before paying.

## Assumptions

- The merchant is the customer of the platform; buyers are the merchant's own customers. There is no marketplace where several sellers share one store.
- MVP scale is 10 stores, 50 products per store and 100 orders per day at peak.
- The primary language is Spanish and dates are shown in a single local time zone; multi-language is out of scope.
- In the MVP, money is collected outside the platform (transfer, deposit or cash) and recorded by staff; online gateways arrive in a later release.
- All amounts are in US dollars with two decimals; converting to another currency is out of scope.
- Every order requires a shipping address, even when the store sells services or digital products.
- The MVP recognises two customer types, retail and wholesale. More types are a configuration change, not a different model.
- Email is the reliable contact channel; SMS is an optional alert channel for the operator.
- Electronic invoicing (SRI) is delivered as a **separate project** that consumes this platform's order data. This platform only reserves the fiscal fields so that no historical migration is needed later.
- Shipment tracking with carriers is out of scope for the MVP; the order states stop at Delivered.
- Only a responsive web experience is in scope; no native mobile application.
- One store is operated by one team; there are no row-level restrictions per user beyond the role model.
