# Dike interface design

## Current implementation — 17 November 2026

The user approved the [finance-focused frontend design](FRONTEND_REDESIGN_PROPOSAL.md). F1/F2 are implemented in the existing .NET 10 Razor application: shared layout and current public, identity, company, staff, NetSuite and Odoo pages. F3–F5 still depend on complete finance ingestion, grants, transactional review and workpapers; no placeholder finance routes were exposed.

The new system uses a 224px ink navy rail (`#142B3D`), cool page surface (`#F6F8FB`), white tables, teal actions (`#127A71`), 28px workspace headings, 14px table/UI text and 8px panel corners. Existing Segoe UI/Tahoma fallbacks, native wordmark/SVGs and public balance photograph are retained. System fonts are the intentional option permitted by the proposal; no external font or frontend framework dependency was added.

The fixed company header and separate Accounts/Connections links follow the selected provider. The overview reads saved Odoo accounts and distinguishes limited previews, missing bank evidence and unavailable period differences. Foreign-currency accounts explain their restriction. Odoo setup uses a three-step list; account selection, weekly/monthly/custom controls, sample debit/credit/signed movement totals and empty-sample messaging share the new components. The public home describes both supported accounting providers. English and Arabic resources contain **434 matching keys**.

Verification: clean seven-project final build with zero warnings/errors; **62 existing HTTP/security smoke checks**, **160 browser checks plus 13 final-build checks**. Playwright/installed Chrome was used because the Browser plugin is absent. English/Arabic pages were checked at widths 1536, 1280, 768, 390 and 320; desktop captures are 1536×1024, phone captures 390×844. No console/page errors or failed resources were recorded in the main browser pass. Tests used isolated companies and synthetic encrypted previews, with no upstream provider calls.

| Fidelity point | Concept / implementation evidence |
| --- | --- |
| Layout | [Accepted account concept](concepts-20261117/accounts-overview.png) and final desktop screenshot inspected with `view_image`; 224px rail, fixed company bar, scope strip, account table and two supporting sections retained. |
| Palette | Computed navy/page/teal colors verified against the accepted tokens; white native panels, no generated textures. |
| Typography and density | 28px workspace heading and 14px table text verified; explicit control sizes and tabular SAR values. |
| Icons and assets | Coherent local outline SVGs, home navigation icon and preserved wordmark; public photograph loaded correctly. |
| Copy and available actions | Reviewed current-state copy and 434-key localization parity; future comparison/review/export actions are intentionally absent. Real coverage labels replace the concept's synthetic completed period. |
| RTL/mobile | Mirrored Arabic shell, isolated dates/amounts, contained table scrolling, responsive forms, working menu/Escape/focus restoration. |

The rendering was faithfully verified against the approved visual direction and its current-feature adaptations. The prototype's completed bank/GL comparison, independent review page and workpapers remain future work, not implemented screenshot replicas. During review, the foreign-currency overview action was corrected to show its restriction. No unresolved material layout issue was found in the checked states; cross-browser and assistive-technology certification were not performed.

Final build: `artifacts/finance-design-final-20261117/`. The app is running at `http://localhost:5080`, PID 36104. QA evidence is outside the repository at `%TEMP%/dike-finance-design-20261117` (`qa-results.json`, `final-results.json`, `overview-final-desktop.png`, `overview-final-ar.png`, `ledger-final-ar-mobile.png`, and public-page captures). The isolated QA server was stopped after verification. The original design record below is historical.

## Historical design — 16 November 2026

Implemented 16 November 2026 with the frontend-app-builder and frontend-testing-debugging skills. The existing ASP.NET Core Razor application remains the application framework.

## Name and purpose

Dike's identity centres on fairness, balance and evidence. The name's association with fair judgement is documented in [Theoi's Dike reference](https://www.theoi.com/Ouranios/HoraDike.html). The interface applies that idea to a precise task: compare booked bank movements with posted NetSuite ledger entries and preserve human judgement.

The design uses calm navigation, consistent controls and clear information hierarchy, a direction also described in [Linear's March 2026 UI refresh](https://linear.app/changelog/2026-03-12-ui-refresh). This is an original Dike identity, built around its actual features.

The current release supports company setup, staff visibility and NetSuite GL collection. Bank imports, comparison, review and evidence exports remain explicitly marked as planned. The homepage and overview contain no invented balances, acceptance results or financial KPIs.

## Implementation references

- [English overview concept](overview-concept.png)
- [Arabic overview concept](overview-ar-concept.png)
- [Public home concept](home-concept.png)
- [Standalone balance photograph](../../src/Dike.Web/wwwroot/images/balance-study.png)

All four images were produced with the built-in ImageGen tool. The photograph is a standalone asset generated from the home concept, not a cropped UI screenshot. The wordmark, monogram, favicon, icons and all interactive content remain native text, SVG and Razor markup.

## Design tokens and components

| Element | Specification |
| --- | --- |
| Navigation and primary actions | Ink navy, #172B36 |
| Page surface | Warm paper, #F7F6F2 |
| Form and table surface | White, #FFFFFF |
| Secondary text | #5F6E76 |
| Rules | #D9D8D0 |
| Active navigation and accents | Bronze, #8B6843 |
| Ready state | Sage background, #EAF0DF, with a text label |
| English display type | Georgia, with serif fallback |
| UI and Arabic type | Segoe UI, Tahoma, Arial, sans-serif |
| Wordmark | Existing lowercase d. / dike treatment, in Georgia, always LTR |
| Spacing | 8, 16, 24, 32 and 40px rhythm; 268px desktop sidebar |
| Containers | 6px corners; open readiness rows and ruled workflows; white panels for forms and tables |
| Icons | Local SVG symbols, 24px, 1.65px strokes; 32px readiness icons |

The [stylesheet](../../src/Dike.Web/wwwroot/dike.css) contains named layers for reset, base, shell, components, pages and responsive rules. Shared elements live in the [layout](../../src/Dike.Web/Pages/Shared/_Layout.cshtml) and [icon partial](../../src/Dike.Web/Pages/Shared/_Icons.cshtml).

Reusable components: public header, authenticated shell, language form, primary/secondary/light buttons, ready/neutral/pending badges, notices, validation summaries, bordered data panels, accessible scroll regions, form rows, numbered steps and empty states. Table amounts use tabular numerals. A small [script](../../src/Dike.Web/wwwroot/dike.js) controls the mobile navigation and authentication-method fieldsets; navigation and both credential groups remain available without JavaScript.

Navigation is limited to the existing overview, connections, company profile, staff and account-security routes. The mobile menu opens below the wordmark, supports Escape and restores focus to its toggle.

## English and Arabic

The header language switch persists the user's selection in the ASP.NET Core culture cookie. Its POST action requires antiforgery validation, accepts only en/ar and returns only to a local URL. Arabic pages declare lang=ar and dir=rtl, mirror the shell and directional icons, and use logical CSS properties.

The [English resources](../../src/Dike.Web/Resources/Localization.UiText.resx) and [Arabic resources](../../src/Dike.Web/Resources/Localization.UiText.ar.resx) contain 281 matching keys. Navigation, forms, status indicators, role labels, help text, page titles, validation and application-owned connector errors are translated. User-entered company/member names and external account/reference data are preserved.

[Localization services](../../src/Dike.Web/Localization/) cover Identity errors, page errors and model-binding messages. Date binding and accounting numbers continue to use the English Gregorian culture in both interfaces; UI culture changes independently. Email addresses, credentials, IDs, dates and amounts are isolated LTR to avoid reordered punctuation or signs.

When adding UI text, add the same key to both resource files and render it through the shared localizer. Keep decimal values and source evidence unchanged. The [web smoke checks](../../tests/registration-smoke.ps1) exercise language persistence, translated validation, secret redaction and redirect protection.

## Deliberate adaptations

The language switch was added after the English concepts at the user's request. Production text uses system fonts and real company/session values; native SVG icons replace generated illustration glyphs. Navy surfaces are flat for consistent rendering. Arabic uses an appropriate sans-serif heading face, and the mobile layout reflows forms and navigation while retaining scrollable tables.

The generated photograph preserves the concept's balance, materials, lighting and framing with small differences in the stone texture and object geometry. No decorative overlays or fake product controls were added.

## ImageGen prompts

The following are the exact prompts used with the built-in ImageGen tool. The overview was generated first; the public home and Arabic overview used it as the image reference. The photograph used the public home concept as its reference.

<details>
<summary>Overview concept</summary>

```text
Create one polished production UI design concept for the existing web application Dike. Exact flat front-on desktop screenshot, 1536 x 1024, no device frame, no perspective, no surrounding moodboard. This is a real company finance workspace: compare booked bank activity with posted NetSuite cash-ledger movements, human judgement and evidence. The name evokes fairness, justice and balance. Brand personality: disciplined, thoughtful, beautifully calm. Modern editorial financial software, sophisticated but highly readable, no gradients, no purple, no fake graphs or financial balances.

Design system: left fixed 232px deep ink navy #172B36 sidebar; main background warm paper #F7F6F2; white #FFFFFF table/panel surfaces; text #20333C; muted #65747B; fine warm grey rules #DDDCD4; muted bronze #9A7447 accents; success soft sage. Squared 6-8px corners, mostly open whitespace and horizontal dividing rules instead of grids of cards. Typography: large elegant Georgia-like serif for lowercase 'dike' wordmark and page headline, precise Segoe UI-like sans UI controls, tabular numbers. Slim custom 1.6px outline icons, no emoji. Brand mark: small native typographic d. monogram beside wordmark, no pictorial logos. Maintain 40px content gutters, broad horizontal rhythm.

Sidebar: brand 'dike' in serif plus small caption 'Reconciliation workspace'; divider; small 'WORKSPACE' caption; active 'Overview' row with dashboard icon, 'Connections' row with linked-lines icon; then 'COMPANY' caption; 'Company profile', 'Staff', 'Account security' rows with coherent outline icons; bottom small 'INITIAL RELEASE', prose 'Clarity. Balance. Evidence.', and working-looking 'Sign out' with logout icon.
Topbar 72px high separated by fine rule, left 'Workspace / Overview', right circular A avatar plus 'admin@example.test' (synthetic placeholder).
Main heading at y125: 'Your close, in clear view.' in 42px serif. Under it 'Example Company · Saudi Arabia · SAR'. Right small outlined 'Company settings' button.
Next full-width dark navy horizontal feature band around y240 height180, subtle fine bronze line accent, no raster art: headline in white serif 'Begin with the evidence.'; body 'Connect NetSuite, collect a posted period, and validate it against your GL report.'; white button 'View connections' with SVG right arrow. Right portion very spare native paired rows 'NETSUITE / Accounting source' and 'BANK / Statement activity', aligned with thin rules; no fake statuses or amounts.
Below heading 'Workspace readiness' and small 'Current release'. Three horizontal full-width rows, separated only by rules: icon + 'Company profile' and 'Administrator account and company details'; right sage outlined status 'Ready'. Second 'NetSuite' and 'No NetSuite data imported'; right neutral 'Not configured' and small arrow. Third 'Bank activity' and 'Bank imports are not yet enabled'; right neutral 'Coming next'. Not a 3-card KPI grid.
Bottom narrow bordered cream strip with title 'From source to sign-off' and horizontal four numbered steps: '01 Connect', '02 Compare', '03 Review', '04 Retain evidence'. Under steps small truthful note 'Comparison, review and evidence exports are planned.' No disabled buttons disguised as actionable.
Footer at bottom, muted small 'Independent review. Dike decisions do not change NetSuite reconciliation status.'
Render crisp exact readable text, rich spacing and professional typography, real implementable HTML structure. No eyebrow above page headline. Show the entire first screen comfortably without tiny text.
```

</details>

<details>
<summary>Public home concept</summary>

```text
Design the PUBLIC HOME PAGE for Dike, using the attached workspace concept's visual identity. One flat production website screenshot, 1536 x 1024, no browser or device frame. Warm paper #F7F6F2, ink #172B36, bronze #9A7447, fine grey rules. Elegant Georgia serif display and precise Segoe UI sans body, 6px corners, very quiet professional finance brand, substantial whitespace. No gradients, decorative pills, hero eyebrows, invented testimonials, stats or claims. Exact copy below.

Header at y0-96 with 72px horizontal padding and fine bottom rule. Left 'd.' bronze serif monogram and 'dike' ink serif wordmark, right small tagline 'Clarity. Balance. Evidence.' then 'Sign in' link and dark 'Create workspace' button with thin outline arrow.
Hero begins y160, two columns with 72px gutters. Left at x72, width650: very large 76px serif headline 'A fair view.' on first line, 'A clearer close.' on second. Below 19px muted body: 'Bring bank activity and your NetSuite ledger into a clearer close. Start with company setup and read-only GL collection.' Primary dark button 'Create your workspace' with arrow, beside text link 'Sign in'. Small underneath 'Your company. Your evidence. Your judgement.'
Hero right x850,width610,height460: beautifully rendered editorial still-life image in a plain rectangle with small 6px radius. Two equally sized pale limestone blocks separated by a small gap, supporting a precisely level thin aged brass beam, on a warm neutral limestone plinth. Minimal architectural sculpture of equilibrium. Side-lit from upper left, natural soft shadow, warm grey studio backdrop, photo realistic matte tactile stone and bronze. No text, no logos, no scales of justice, no tech symbols. It should feel like a high-end brand photograph, not a UI drawing.
Below hero around y710, fine horizontal line. Large serif title 'Two sources. Human judgement.' with small right text 'The Dike approach'.
Below this title a 3-column OPEN layout separated by vertical fine rules (no cards). Columns: bronze tiny number01, heading 'Establish the facts', description 'Collect posted NetSuite movements within a clear company and period scope.'; 02 'Examine the differences', description 'Bank comparison and explainable proposals are the next step.'; 03 'Keep the reasoning', description 'Independent review and evidence workpapers are planned.'
At bottom short release line with sage tiny dot: 'Available now: company setup, staff directory and NetSuite GL collection.' and underneath muted 'Bank imports, comparison, review and exports are not yet enabled.'
Make all real copy readable, natural line heights, precisely balanced composition. Preserve brand and type character of reference, but this is the anonymous home page not the sidebar app.
```

</details>

<details>
<summary>Standalone balance photograph</summary>

```text
Create the standalone photograph used inside the right-hand hero image in the supplied Dike homepage concept. Output ONLY the editorial still-life photograph, no UI, no text, no borders, no webpage, no typography, no logo. Wide landscape 1536 x 1024. Two equally sized cuboid pale cream limestone blocks, separated by a clear gap, supporting one perfectly level, thin, aged brushed brass beam that spans both supports. Arrange symmetrically with physically convincing contact and balance. Place on a large pale limestone plinth, with a seamless warm grey stone studio background. Soft natural light from upper left, slightly longer gentle shadow to lower right. Matte tactile stone with subtle natural pores, beautiful refined bronze patina. Architectural sculpture of balance and fairness. Match the reference photograph's camera angle, object geometry, soft light, warm neutral palette, minimal composition and realism; leave about 20% clear space above the beam. Nothing levitating, no props, no plants, no books, no scales, no symbols. High-end editorial brand photography.
```

</details>

<details>
<summary>Arabic overview concept</summary>

```text
Create the ARABIC RTL version of the attached Dike overview design. One flat desktop application screenshot 1536 x 1024. Preserve the exact restrained navy #172B36, paper #F7F6F2 and bronze visual system and content hierarchy. Mirror the layout properly: fixed 268px navy sidebar on RIGHT, main content on LEFT, right-aligned Arabic content, icons and row arrows mirrored, RTL breadcrumb. Latin 'd. dike' wordmark stays Latin. Beautiful readable contemporary Arabic sans typography (Segoe UI / Tahoma-like), no faux calligraphy. No gradients, no added charts or metrics.
Sidebar from top: 'd. dike', 'مساحة عمل المطابقة', then section 'مساحة العمل'; active 'نظرة عامة', 'الاتصالات'; section 'الشركة'; 'ملف الشركة', 'فريق العمل', 'أمان الحساب'; bottom 'الإصدار الأول', 'وضوح. توازن. أدلة.', 'تسجيل الخروج'.
Topbar breadcrumb 'مساحة العمل / نظرة عامة'. Left side user admin@example.test in LTR and small English language switch.
Page heading on right of content: 'إقفال مالي برؤية أوضح.'; subtitle 'شركة المثال · المملكة العربية السعودية · SAR'; opposite outlined settings button 'إعدادات الشركة'.
Dark navy feature band right heading 'ابدأ بالأدلة.'; supporting 'اربط NetSuite، واجمع حركات فترة مُرحّلة، ثم تحقّق منها بمقارنتها بتقرير الأستاذ العام.'; pale action button 'عرض الاتصالات' with leftward arrow. Left of band paired source labels 'NETSUITE / المصدر المحاسبي' and 'البنك / حركات كشف الحساب'.
Section heading 'جاهزية مساحة العمل' and small 'الإصدار الحالي'.
Three open ruled readiness rows right-to-left: company outline icon, 'ملف الشركة', 'حساب المسؤول وبيانات الشركة', sage status 'جاهز'; layered ledger icon, 'NetSuite', 'لم تُستورد بيانات من NetSuite', neutral status 'غير مهيأ', left arrow; bank outline icon, 'حركات البنك', 'استيراد حركات البنك غير متاح بعد', status 'قريبًا'.
Bottom outlined workflow strip heading 'من المصدر إلى الاعتماد', four RTL numbered steps '01 الربط', '02 المقارنة', '03 المراجعة', '04 حفظ الأدلة'; muted note 'المقارنة والمراجعة وتصدير الأدلة ضمن الخطة.'
Footer 'مراجعة مستقلة. قرارات Dike لا تغيّر حالة المطابقة في NetSuite.'
This is a production localization of the same interface. Balanced generous spacing, readable exact Arabic, keep Latin identifiers LTR, no extra decorative labels.
```

</details>
