# Pocket Drive — Technical Requirements

Version: 0.1 — discussion draft  
Date: 27 September 2026  
Status: Proposed requirements for review; not an approved implementation specification.

## 1. Purpose and current state

Build a professional Unity driving game with a sustainable commercial model. The experience must be enjoyable without purchases and reliable enough to protect player progress and paid entitlements.

The workspace now contains a Unity 6000.6.3f1 project targeting Android, with a basic driving sandbox and development build automation. Original concept images and icons remain in `outputs/`. This is a new Unity implementation, rather than a source-code migration. Commercial services described below are not yet implemented.

User-requested capabilities: login, ads, subscriptions, daily banners, notifications, daily prizes, and purchases of game currency. Their exact implementation and release timing remain open.

Android is the confirmed target; iOS is excluded. Audience age, launch countries, core driving mode, budget, and schedule have not been confirmed. Requirements below are proposals unless identified as user-requested.

## 2. Product decisions required before implementation

| Decision | Proposed starting point | Why it matters |
|---|---|---|
| Main activity | Select one: racing, drifting, parking, or exploration | Determines controls, levels, scoring, progression, and content cost |
| Platform | Android confirmed; test phone model and Android version TBD | Determines test devices, billing, sign-in, and release work |
| Audience and age range | Explicitly define before integrating ads or analytics | Affects privacy, ad configuration, purchases, and presentation |
| Driving style | Accessible arcade handling | Must be validated with a playable prototype |
| Visual style | Stylized; review existing artwork | Sets asset and performance budgets |
| Session length | Short mobile sessions; exact target TBD | Determines challenge and reward design |
| Connectivity | Offline basic driving; online economic transactions | Separates play availability from economy integrity |
| Multiplayer | Excluded from initial scope | Real-time multiplayer materially changes architecture and cost |
| Daily banners | Assume in-game event/promotion cards | Confirm whether the user instead means advertising banners |
| Subscription | Later release after recurring content is proven | Requires an ongoing production and support commitment |

## 3. Delivery phases

### Phase A — playable prototype

- Create the Unity project with a pinned stable editor version and compatible packages.
- Implement one vehicle, one compact environment, touch controls, follow camera, basic audio, pause, and restart.
- Implement one repeatable challenge appropriate to the chosen driving mode.
- Test handling and performance on the primary target device.
- Exit criterion: the core activity is understandable and enjoyable in external playtests; performance meets the agreed device target.

### Phase B — production foundation

- Add onboarding, garage, progression, local saves, settings, guest identity, account linking, and cloud-save rules.
- Add backend environments, economy records, diagnostics, analytics, and remote configuration.
- Build consistent UI, loading/error states, accessibility options, and recovery flows.
- Exit criterion: progress survives supported restart, upgrade, account-link, and connectivity scenarios without silent loss.

### Phase C — limited commercial release

- Add a small catalog of store purchases and optional rewarded ads.
- Add daily rewards, a rotating challenge, promotional cards, and opt-in notifications.
- Complete purchase, refund, consent, privacy, account deletion, and support flows.
- Release to a limited audience and assess retention, stability, economy balance, and revenue against operating costs.
- Exit criterion: agreed reliability and product metrics are met; no unresolved critical purchase or data-loss defects.

### Phase D — ongoing content and expansion

- Add more vehicles, environments, challenges, and scheduled events.
- Introduce a subscription only with a defined recurring benefit schedule and operational ownership.
- Consider additional platforms and social features after reviewing costs and evidence from the initial release.

No calendar estimates are committed until scope, team, assets, and budget are known.

## 4. Functional requirements

### 4.1 Driving and game session

- Support acceleration, braking/reverse, steering, and reset-to-track. Drift or handbrake controls depend on the chosen mode.
- Keep vehicle handling configurable separately from presentation and UI.
- Evaluate Unity WheelCollider against a custom arcade controller during the prototype; do not commit solely on realism claims.
- Run physics at a consistent simulation interval and verify behavior at different render frame rates.
- Handle app backgrounding, pause/resume, interruptions, and device rotation policy explicitly.
- Define challenge completion, failure, scoring, and reward eligibility in a versioned ruleset.
- Use original or appropriately licensed vehicles, logos, music, sounds, and environment assets.

### 4.2 Screens and navigation

Required screens: boot/loading, onboarding, home, garage, gameplay HUD, pause, results, shop, daily rewards, profile/account, settings, and support/legal.

Later screens: subscription details, events, and subscription-management links.

- Respect device safe areas and support localized text without truncation.
- Show loading, empty, unavailable, success, and recoverable failure states.
- Provide audio controls, steering sensitivity, and reduced camera-shake options.
- Keep offers and ads out of active driving and avoid obstructing core controls.

### 4.3 Identity and accounts

- Allow guest play without a mandatory registration gate.
- Offer supported account providers after platforms are selected; avoid a custom password system unless justified.
- Give each backend player a stable internal identifier independent of provider identifiers.
- Explain that unlinked guest progress may not survive reinstall or device loss.
- Support secure account linking, sign-out, recovery guidance, and deletion.
- Define conflict behavior when linking to an account that already has progress; do not silently overwrite either save or merge currency balances.
- Reauthenticate sensitive account actions when appropriate; rate-limit authentication and linking endpoints.
- Explain subscription cancellation separately from account deletion.

### 4.4 Saving and progression

- Store local gameplay settings and an offline progress cache with schema versions and migrations.
- Save at checkpoints and session completion using atomic replacement or equivalent corruption protection.
- Keep a last-known-good save and expose a recovery path.
- Track cars, cosmetics, progression milestones, and best scores separately from paid balances and entitlements.
- Define field-specific cloud conflict resolution; device timestamps alone must not determine the winning save.
- Permit offline practice. Define whether offline challenge rewards are capped, deferred for validation, or unavailable before implementing offline progression.
- Prevent replayed offline sessions from granting rewards more than once.

### 4.5 Economy and purchases

- Start with one earned currency. Decide whether purchased currency is the same balance or a separate premium currency before launch.
- Define earning rates, item prices, sinks, purchase limits, and progression pacing in versioned configuration.
- Display store-provided localized prices and exact item contents before purchase.
- Use the applicable platform billing integration for digital purchases; verify regional rules before release.
- Verify purchases on the backend before granting currency or entitlements.
- Maintain an append-only economy ledger with unique transaction IDs and reason codes.
- Make grants idempotent: retries, duplicate callbacks, and repeated receipts must not create duplicate value.
- Support pending, cancelled, failed, completed, restored, refunded, and revoked purchase states.
- Handle platform acknowledgement/consumption requirements and delayed fulfillment; reconcile missed store notifications.
- Restore eligible entitlements. Consumable balances must be recovered through the account ledger rather than assumed to be restored by the store.
- Establish a documented refund policy for already-spent currency and never silently delete unrelated progress.
- Exclude paid randomized rewards from initial scope.

### 4.6 Ads

- Initial format: explicitly chosen rewarded ads outside active gameplay.
- State the reward before showing an ad; grant it only after verified completion using provider-supported server verification where available.
- Deduplicate reward callbacks and enforce configurable reward limits.
- Handle no-fill, slow load, cancellation, backgrounding, and SDK failure without blocking ordinary play.
- Apply consent and age-related configuration before starting SDK behavior that depends on it.
- Make ad placements remotely disableable.
- If forced ads or an ad-removal purchase are added later, define their precise behavior and clearly state whether optional rewarded ads remain available.

### 4.7 Daily prizes and challenges

- Use backend time and a documented reset schedule, initially proposed as a single UTC reset.
- Permit each eligible reward to be claimed once per period through an atomic backend transaction.
- Show reset timing clearly in the UI and handle daylight-saving changes without changing eligibility.
- Keep daily rewards modest enough that skipping a day does not block normal progression.
- Version challenge definitions and record the version used for completion and rewards.
- Define fraud checks for challenge submissions. Server ownership of the wallet alone does not make client-reported gameplay scores trustworthy.

### 4.8 Promotional cards and notifications

- Support scheduled home-screen cards with title, approved image, destination, start/end dates, and eligibility rules.
- Validate destinations and configuration; do not execute arbitrary code or open untrusted URLs from remote content.
- Provide cached fallback content and hide expired or unavailable offers.
- Ask for notification permission in context, after explaining the benefit.
- Offer categories, opt-out controls, quiet hours, and frequency limits; a proposed starting cap is one promotional notification per day.
- Keep transaction/support messages distinct from marketing preferences.
- Handle expired notification destinations gracefully and unregister tokens after sign-out or deletion as appropriate.

### 4.9 Subscription — later phase

- Define a concrete monthly content and benefit schedule before implementing billing.
- Candidate benefits: new cosmetic content, garage customization, and member challenges. Final benefits and pricing are TBD.
- Clearly display recurring price, billing period, benefits, trial terms if any, and cancellation guidance.
- Treat subscription access as a server-maintained entitlement based on verified store state.
- Handle renewal, cancellation, expiration, grace period, billing retry, refunds, revocation, and purchase restoration.
- Distinguish cancellation from expiration: benefits continue through the verified paid entitlement period unless revoked.
- Specify which claimed cosmetics remain owned after expiration and which temporary benefits end.
- Avoid making core progression or competitive success dependent on membership.

## 5. Proposed technical architecture

### Unity client

- C# with clear modules for driving, session rules, UI, saves, identity, economy, ads, billing, analytics, notifications, and remote configuration.
- Use a mobile-appropriate render pipeline; proposed default is URP, subject to device profiling.
- Keep SDK integrations behind small interfaces so providers can be replaced and failure paths tested.
- Use configuration assets for local tuning; distribute server-controlled economy and event settings through validated remote configuration.
- Never embed backend administrative credentials or treat obfuscated client values as trusted security controls.

### Backend

Provider is not selected. Compare Unity Gaming Services, Firebase plus server functions, PlayFab, or a small custom service against authentication, transactional economy, receipt verification, backups, observability, operating cost, and team familiarity. Verify current capabilities and pricing before choosing.

Required responsibilities:

- Authentication and linked identities.
- Cloud progression and conflict handling.
- Atomic wallet/ledger updates and inventory ownership.
- Purchase verification, store-event ingestion, and reconciliation.
- Daily reward eligibility, reward deduplication, and subscription state.
- Versioned catalog, events, configuration, and feature flags.
- Notification preferences and delivery orchestration.
- Support tools, audit history, deletion jobs, and operational metrics.

Use separate development, staging, and production environments. Keep secrets in a server-side secret store. Restrict administrator access by role and require strong authentication.

### Minimum data entities

| Entity | Essential fields |
|---|---|
| Player | Internal ID, linked providers, creation time, status |
| Progress | Player ID, schema/rules version, milestones, revision |
| Wallet | Player ID, currency type, balance, revision |
| Ledger entry | Transaction ID, player ID, delta, reason, source ID, server time |
| Inventory | Player ID, item ID, acquisition source, ownership status |
| Purchase | Store, store transaction ID, product ID, player ID, verification/fulfillment state |
| Subscription | Store reference, player ID, entitlement, expiration, verified state |
| Reward claim | Player ID, reward period/event ID, unique claim key, grant transaction |
| Configuration | Version, validity window, minimum compatible client, payload |
| Preferences | Player ID, consent version, notification categories, locale |

All privileged mutations require authenticated authorization, input validation, rate limits, and audit records. Wallet grants and their ledger records must commit atomically.

## 6. Reliability, performance, and security

These are proposed acceptance targets, to be confirmed after selecting test devices:

- Stable 30 FPS on the agreed minimum device; target 60 FPS on the primary device, with quality settings as needed.
- No crashes or unrecoverable freezes during a 30-minute representative gameplay test, including a thermal-performance check.
- Proposed limited-release crash-free session target: at least 99.5%, reported with sample size and measurement window.
- Set explicit startup, memory, installation-size, and network budgets after measuring the first representative build; no invented budgets before asset scope is known.
- Network requests must time out, show recoverable errors, and use bounded retries with backoff.
- Retrying any economic operation must never duplicate a grant or debit.
- Protect credentials using platform-appropriate secure storage; use TLS for network traffic and redact tokens/receipts from routine logs.
- Recover from interrupted saves, corrupted local data, backend outages, delayed purchases, and stale remote configuration.
- Back up server data and perform a restore drill before commercial launch; agree recovery-time and recovery-point targets.
- Keep basic play available when optional ads, analytics, or promotional services are unavailable.
- Provide kill switches for purchases, ads, reward claims, and faulty events, without revoking already-owned content unnecessarily.

## 7. Analytics and commercial evaluation

Define a documented event schema covering onboarding, session start/end, challenge attempts/completion, garage unlocks, currency sources/sinks, shop views, purchases, rewarded ads, daily claims, and subscription lifecycle events.

- Use backend-confirmed purchase and grant records as the economic source of truth.
- Deduplicate events and avoid collecting unnecessary personal data.
- Measure day-1/day-7/day-30 retention by acquisition cohort, session length, challenge difficulty, crash/ANR rates, purchase conversion, ad engagement, and revenue per active user.
- Calculate net contribution after store fees, refunds, advertising spend, backend costs, and content/support costs.
- Agree success thresholds before a limited release. Do not assume revenue targets or profitability without observed player data.

## 8. Privacy, store readiness, and support

- Confirm audience age, launch regions, data collection, retention periods, ad settings, and consent requirements before SDK selection.
- Publish privacy policy, terms, support contact, and accurate store privacy/data disclosures.
- Implement account deletion across identity, cloud saves, notification tokens, and linked services, with explicit handling of records that must be retained.
- Provide clear subscription-management and purchase-recovery help.
- Maintain asset licenses and avoid unlicensed real-world car branding.
- Check current store policies at release; regional payment rules and provider requirements can change.

Reference policies consulted for this discussion:

- Apple in-app purchases: https://developer.apple.com/in-app-purchase/
- Apple account deletion: https://developer.apple.com/support/offering-account-deletion-in-your-app/
- Google Play payments: https://support.google.com/googleplay/android-developer/answer/9858738?hl=en
- Google Play subscriptions: https://support.google.com/googleplay/android-developer/answer/9900533?hl=en

## 9. Verification and release acceptance

Automate critical economy and entitlement tests; use device playtests for handling, visual quality, interruptions, and performance.

Required scenarios before commercial release:

1. Fresh install, guest play, account linking, sign-out, and returning login.
2. Link to an existing account with conflicting progress without silently losing value.
3. Restart, app upgrade, corrupted cache, supported reinstall recovery, and cross-device sync.
4. Offline gameplay followed by reconnect, including replayed reward submissions.
5. Purchase success, cancellation, pending payment, network loss after charge, duplicate callback, restoration, refund, and delayed store event.
6. Subscription renewal, cancellation, expiration, grace period, and revocation if subscriptions are included.
7. Rewarded ad completion, dismissal, no-fill, duplicate callback, and SDK failure.
8. Daily claim from two devices simultaneously, changed device clock, and reset-boundary claims.
9. Notification refusal, opt-out, expired deep link, and account deletion.
10. Backend outage, invalid configuration, configuration rollback, and disabled monetization placements.

Release gates: no unresolved critical data-loss or purchase defects; supported-device performance verified; store sandbox flows passed; privacy disclosures completed; support and rollback procedures documented.

## 10. Development workflow and deliverables

- Commit Unity assets and `.meta` files; exclude generated directories such as `Library`, `Temp`, build output, and secrets.
- Enable text serialization and use large-file storage where appropriate for binary assets.
- Pin editor/package versions and document reproducible local and CI builds.
- Keep development credentials and store sandbox products separate from production.
- Deliver source, backend configuration/schema, deployment instructions, test results, operational runbook, economy design, and asset-license inventory.
- Use staged release and maintain a compatible backend while older client versions remain supported.

## 11. Questions for Claude's review

1. What should the smallest commercially credible version contain, and what should be deferred?
2. Which driving mode and progression loop best fit a small team's content budget?
3. Which Unity vehicle approach is appropriate for that mode, and how should we compare prototypes?
4. Which backend minimizes complexity while supporting atomic economy operations, account linking, purchase verification, and deletion?
5. What trust boundaries and failure scenarios are missing from the economy and offline design?
6. Should we start with one currency or two, and what initial earning/spending model should we test?
7. Can a subscription offer enough recurring value within our content-production capacity?
8. What realistic staffing, service costs, and milestones follow once platform and scope are chosen?
9. Which requirements are excessive for the first release, and which missing requirements are release blockers?

Suggested review prompt:

> Review this Pocket Drive requirements draft as a Unity technical lead and mobile-game product architect. Separate confirmed needs from assumptions. Challenge unnecessary complexity, identify missing decisions and failure cases, and propose a lean first release with a phased architecture. Compare backend options using current official documentation and clearly state uncertainties. Do not assume a team size, budget, gameplay mode, or revenue target. Start with the highest-impact questions before estimating implementation work.
