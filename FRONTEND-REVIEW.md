# HTML, Bootstrap, JavaScript and CSS review

Reviewed 20 October 2026 against Bootstrap 5.3 and MDN guidance. This is a focused implementation review, not a complete WCAG certification or performance benchmark.

## Changes

- Fixed active wizard-step text contrast: the custom dark green background previously retained a dark custom link color; active text is now white.
- Expanded visible keyboard focus to inputs, selects, textareas and focusable scroll regions. Added a forced-colors active-step/error outline, narrow-layout minimum-width resets and wrapping for long content. Existing reduced-motion rules remain.
- Named responsive table regions now have tabindex=0 so keyboard users can scroll wide tables. Added captions to property/unit and review-history tables. Kept real table/header semantics and Bootstrap's responsive wrapper.
- Shared MVC Tag Helpers link styled editable fields to their validation messages using prefix-aware IDs, preserve existing descriptions and set aria-invalid only after server validation fails. Hidden/checkbox/radio fields are excluded. Filter selects opt out because they do not have validation-message spans. New validated form controls should include their matching asp-validation-for span; nonvalidated filters use data-no-field-validation.
- Added mobile phone input hints without changing the accepted phone format.
- Modal saves announce Saving through a polite live status, expose aria-busy, disable dismissal buttons and prevent backdrop/Escape dismissal until the POST finishes. The busy state always clears after completion/failure. This avoids a late save result replacing a different open dialog.
- Modal content changes call Bootstrap handleUpdate. Focus prefers invalid controls, then an editable field, then a button for confirmation-only forms. Existing stale-GET cancellation and duplicate-POST guards remain.
- Removed the unused status-dot CSS rule after adopting Font Awesome status icons.

No library upgrades, additional third-party JavaScript, database changes, role changes or CDN-policy changes were needed. CDN-first assets with local fallbacks and license notices remain intact. No blanket defer/async was added because existing dependency/fallback scripts require ordered execution.

## Validation

HTTP regression tests cover validation descriptions, server-invalid state, named table regions and modal live status. Existing template tests cover nested name prefixes and validation IDs. The JavaScript harness now also checks busy state, blocked dismissal and restored close/submit controls, in addition to stale/closed reads and duplicate POSTs. Real-browser smoke checks verify the modal and active wizard presentation; responsive and screen-reader audits can be expanded in a future browser test suite.

## References

- [Bootstrap modal lifecycle, focus and handleUpdate](https://getbootstrap.com/docs/5.3/components/modal/)
- [Bootstrap accessibility guidance](https://getbootstrap.com/docs/5.3/getting-started/accessibility/)
- [ARIA invalid state](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-invalid)
- [ARIA descriptions](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-describedby)
- [ARIA busy state](https://developer.mozilla.org/en-US/docs/Web/Accessibility/ARIA/Reference/Attributes/aria-busy)
- [Keyboard-accessible overflow](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/overflow)
- [Reduced-motion preferences](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@media/prefers-reduced-motion)
- [HTML autofill semantics](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/autocomplete)

Final verification: build passed; 68 tests passed, one opt-in SQL Server test skipped; JavaScript syntax/state checks passed. Browser verified wizard colors, named scroll region, valid modal description targets, opening/dismissal and no console warnings/errors.
