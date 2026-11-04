# Design system — Saga-Sales panel

Palette and usage rules for the Blazor panel. Tokens live in
`src/SagaSales.BlazorWeb/wwwroot/css/app.css` (`:root`), so a change there propagates to every
page without touching markup.

## Palette

| Token | Hex | Role |
|---|---|---|
| `--ink` | `#0b0f1a` | Page background. The anchor: nothing sits on a lighter canvas. |
| `--navy` | `#0f2a4f` | Deep surface. Used for borders, elevated rows and secondary buttons — never as a full-bleed fill. |
| `--blue` | `#1f6feb` | **Action.** Primary buttons, links, active nav, chart bars, focus tint. |
| `--cyan` | `#2dd4ff` | **State only.** Status chips, the current period marker, focus rings, positive deltas. |
| `--paper` | `#e6eefc` | Text. Headings at full strength, body slightly muted. |

Two functional colours were added because the palette has no way to express a failure:
`--warn` (`#e6b45c`) for pending/low stock and `--danger` (`#f26d6d`) for failed/cancelled/errors.
They are used **only** on state, never on actions or decoration.

## The rule that governs everything

**Cyan is reserved for state.** If a cyan element is not telling the user *something has a
condition*, it is in the wrong place. Concretely:

- A primary button is blue. A *successful* result is cyan.
- A chart series is blue. The bar for the *current* period gets a cyan outline.
- A heading is paper. A *live* indicator is cyan.

That reservation is what keeps the accent loud: the moment cyan is used for a button, it stops
meaning anything.

## Tokens in practice

| Layer | Value | Notes |
|---|---|---|
| Page | `--ink` | Plus a 380 px radial glow of blue at 10 % at the top, so the ink does not read as flat black |
| Panel | `--surface-1` (`#0e1522`) | 12 px radius, 1 px `--line` border, no heavy shadow |
| Panel head | transparent | 1 px bottom border separates title from content |
| Inputs | `--surface-2` | Focus: blue border + 3 px blue tint |
| Tables | transparent | Uppercase 11 px headers, tabular figures in numeric columns |
| Chips | tinted on translucent | `chip--ok` cyan · `chip--info` blue · `chip--warn` amber · `chip--danger` red · `chip--neutral` paper |

## Typography

`Inter` (400/500/600/700), falling back to Segoe UI and system-ui. Headings use
`letter-spacing: -0.02em`; KPI values `-0.03em` and `tabular-nums` so digits do not jitter when
numbers update.

## Layout

Sidebar of 232 px with the brand, one nav group and a sign-out footer; sticky topbar with the page
title and its subtitle; content capped at 1320 px. Under 900 px the sidebar becomes a horizontal
icon rail, which is what makes the same markup usable on a tablet.

## Components available

`.panel` / `.panel__head` / `.panel__body` · `.kpi` + `.kpi__label` / `.kpi__value` / `.kpi__meta` ·
`.chip--*` · `.stat-row` · `.toolbar` · `.page-head` + `.page-head__sub` · `.empty` ·
`.chart-bars` / `.chart-bar` / `.chart-bar--current` · `.legend` · `.divider` · `.delta--*`

Bootstrap is still loaded, but only for its grid (`row`, `col-*`) and spacing utilities; its theme
variables are overridden in `app.css` so existing markup inherits the dark palette automatically.

## Things to avoid

- Pure `#000` or pure `#fff`: they flatten the nautical depth.
- Cyan on a button, a link or a chart series.
- More than one accent per panel: a KPI card carries a blue value and, at most, one chip.
- Heavy box-shadows: depth comes from the border and the surface step, not from blur.
