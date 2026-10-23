# Bootstrap component review and UI decisions

Reviewed the complete Bootstrap 5.3 component catalog against the Bootstrap 5.3.8 library on 2026-10-20. Bootstrap CSS and the Popper-inclusive JavaScript bundle prefer the official version-pinned jsDelivr CDN, with matching local fallbacks. CDN requests use SHA-384 integrity, anonymous CORS and no-referrer. No external font or image dependency was introduced.

| Component | Decision for this application |
| --- | --- |
| Accordion | Deferred; the short application sections already have explicit step navigation. |
| Alerts | Persistent form errors, workflow notices, dismissible success/info notices. |
| Badge | Shared status partial, visible status text and color; availability indicator. |
| Breadcrumb | Application detail back-navigation and current location. |
| Buttons | Consistent primary, secondary, small row actions and destructive-action styling. |
| Button group | Deferred; ordinary wrapping action rows work better on narrow screens. |
| Card | Status totals, filter panel, results, property/unit cards and account forms. |
| Carousel | Omitted; rotating content does not help applicants complete tasks. |
| Close button | Notices and modal dialogs. |
| Collapse | Accessible mobile navbar toggle. |
| Dropdowns | Native select controls are clearer for status, property and title selection. |
| List group | Deferred; current structured details and tables fit the data better. |
| Modal | Existing edit/review workflows, centered scrollable dialogs, focus handling. |
| Navbar | Responsive role-aware navigation, current-page state and account identity. |
| Navs and tabs | Server-rendered wizard section links retain ordinary navigation semantics. |
| Offcanvas | Deferred; collapse navigation is sufficient at the current menu size. |
| Pagination | Deferred; adding it requires a separate database paging change. No fake client pagination. |
| Placeholders | Deferred; dialogs use a real loading spinner for short fetches. |
| Popovers | Omitted; essential guidance stays visible. |
| Progress | Deferred; current step is not the same as saved/submitted completion. |
| Scrollspy | Omitted; pages do not have long anchored documentation sections. |
| Spinners | Modal loading state with a textual status announcement. |
| Toasts | Deferred; persistent notices are easier to discover than disappearing feedback. |
| Tooltips | Omitted for required instructions; controls have visible labels. |

Forms use visible labels, existing unobtrusive client validation and server validation. Tables scroll within their own container on small screens. A skip link, current-page/step semantics, focus outlines, accessible modal headings, text status labels and reduced-motion rules support keyboard and assistive-technology use. Bootstrap does not by itself establish full accessibility compliance.

The visual system uses warm neutral surfaces, deep green actions, consistent spacing, serif page headings and compact system-font body text. Anonymous home remains generic; authenticated lists and role-based access are unchanged. Counts use existing role-scoped database aggregates; no additional database queries were introduced for the redesign.

Sources:
- https://getbootstrap.com/docs/5.3/components/navbar/ (catalog and responsive navigation)
- https://getbootstrap.com/docs/5.3/components/card/
- https://getbootstrap.com/docs/5.3/components/modal/
- https://getbootstrap.com/docs/5.3/components/badge/
- https://getbootstrap.com/docs/5.3/components/navs-tabs/
- https://getbootstrap.com/docs/5.3/forms/overview/
- https://getbootstrap.com/docs/5.3/content/tables/

## CDN loading and fallback

Upgraded all bundled CSS/JS distributions and source maps from the official bootstrap 5.3.8 npm archive. The CSS and bundle hashes match the official introduction documentation. ASP.NET Core Link/Script Tag Helpers emit local fallbacks: CSS tests Bootstrap's visually-hidden position:absolute rule; JavaScript tests the Bootstrap Modal and Collapse APIs. Bootstrap loads before site.js, without async/defer, so its generated fallback completes before modal initialization. The fallback uses the identical bytes and retains integrity verification. The local theme always loads after Bootstrap. CDN URLs are version-pinned rather than mutable latest URLs.

Official version and hashes: https://getbootstrap.com/docs/5.3/getting-started/introduction/

The same CDN/fallback policy now covers jQuery and both validation libraries. Validation is included only once globally. jQuery remains at 3.7.1 because the current unobtrusive adapter depends on APIs removed by jQuery 4; Validation is updated to 1.22.1. `.gitattributes` preserves vendor bytes so Windows line-ending conversion cannot invalidate local SRI. Runtime hashes and URLs are recorded in `client-assets.json`.

## HTML/template consolidation

Used ASP.NET Core 10's shared DisplayTemplates/EditorTemplates conventions rather than building a custom HTML rendering framework. PersonName is a strongly typed editor backed by common input metadata; ApplicantInformation, ApplicationStatus and UtcDate are shared display templates. Templates preserve field prefixes, validation and Razor encoding. Modal headers/footers remain partials because they describe actions rather than a displayed data type.

Combined Bootstrap card/card-body on the same element for simple cards, account panels and the application form. Kept separate bodies where headers and table regions need independent layout. Shared CSS handles form-field spacing, validation colors, action rows, account submit width and read-only definition lists. Retained Bootstrap grid and control classes where they express actual responsive/component semantics. Empty read-only residence action columns are omitted.

Reference: https://learn.microsoft.com/en-us/aspnet/core/mvc/views/display-templates?view=aspnetcore-10.0
